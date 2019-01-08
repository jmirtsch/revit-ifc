//
// Revit IFC Import library: this library works with Autodesk(R) Revit(R) to import IFC files.
// Copyright (C) 2013  Autodesk, Inc.
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Revit.IFC.Common.Utility;
using Revit.IFC.Common.Enums;
using Revit.IFC.Import.Geometry;
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   /// <summary>
   /// Represents the object placement.
   /// </summary>
   public static class IFCLocation
   {
      public static Transform TotalTransform(this IfcObjectPlacement placement)
      {
         IfcLocalPlacement localPlacement = placement as IfcLocalPlacement;
         if (localPlacement != null)
            return localPlacement.TotalTransformLocalPlacement();

         Importer.TheLog.LogError(placement.StepId, "Placement not implemented", true);
         return Transform.Identity;
      }
      public static Transform TotalTransformLocalPlacement(this IfcLocalPlacement placement)
      {
         IfcObjectPlacement relativeTo = placement.PlacementRelTo;
         if (relativeTo == null)
            return placement.RelativePlacement.GetAxis2PlacementTransform();
         return relativeTo.TotalTransform().Multiply(placement.RelativePlacement.GetAxis2PlacementTransform()); 
      }

      internal static Transform GetPlacementTransformUnscaled(this IfcPlacement placement)
      {
         return Transform.CreateTranslation(placement.Location.ProcessIFCCartesianPoint());

      }
      internal static Transform GetPlacementTransform(this IfcPlacement placement)
      {
         IfcCartesianPoint location = placement.Location;
         return Transform.CreateTranslation(location.ProcessScaledLengthIFCCartesianPoint());
      }

      internal static Transform GetAxis2Placement2DTransform(this IfcAxis2Placement2D placement)
      {
         IfcDirection refDirection = placement.RefDirection;
         XYZ refDirectionX = refDirection == null ? XYZ.BasisX : IFCPoint.ProcessNormalizedIFCDirection(refDirection);
         XYZ refDirectionY = new XYZ(-refDirectionX.Y, refDirectionX.X, 0.0);

         Transform lcs = placement.GetPlacementTransform();
         lcs.BasisX = refDirectionX;
         lcs.BasisY = refDirectionY;
         lcs.BasisZ = refDirectionX.CrossProduct(refDirectionY);

         return lcs;
      }

      internal static Plane GetPlane(this IfcAxis2Placement3D placement)
      {
         XYZ origin = XYZ.Zero, x = XYZ.BasisX, y = XYZ.BasisY;
         Transform transform = GetAxis2Placement3DTransform(placement);
         origin = transform.OfPoint(origin);
         x = transform.OfPoint(x);
         y = transform.OfPoint(y);
         return Plane.CreateByThreePoints(origin, x, y);
      }
      internal static Transform GetAxis2Placement3DTransform(this IfcAxis2Placement3D placement)
      {
         return placement.getAxis2Placement3DTransform(true);
      }
      internal static Transform GetAxis2Placement3DTransformUnscaled(this IfcAxis2Placement3D placement)
      {
         return placement.getAxis2Placement3DTransform(false);
      }
      private static Transform getAxis2Placement3DTransform(this IfcAxis2Placement3D placement, bool scale)
      {
         if (placement == null)
            return Transform.Identity;
         Transform lcs = scale ? placement.GetPlacementTransform() : placement.GetPlacementTransformUnscaled();
         IfcDirection axis = placement.Axis;
         XYZ axisXYZ = axis == null ?  XYZ.BasisZ : IFCPoint.ProcessNormalizedIFCDirection(axis);
         IfcDirection refDirection = placement.RefDirection;
         XYZ refDirectionXYZ = refDirection == null ?  XYZ.BasisX : IFCPoint.ProcessNormalizedIFCDirection(refDirection);

         XYZ lcsX = (refDirectionXYZ - refDirectionXYZ.DotProduct(axisXYZ) * axisXYZ).Normalize();
         XYZ lcsY = axisXYZ.CrossProduct(lcsX).Normalize();

         if (lcsX.IsZeroLength() || lcsY.IsZeroLength())
         {
            Importer.TheLog.LogError(placement.StepId, "Local transform contains 0 length vectors", true);
         }   

         lcs.BasisX = lcsX;
         lcs.BasisY = lcsY;
         lcs.BasisZ = axisXYZ;
         return lcs;
      }

      /// <summary>
      /// Convert an IfcAxis1Placement into a transform.
      /// </summary>
      /// <param name="placement">The placement handle.</param>
      /// <returns>The transform.</returns>
      public static Transform GetIFCAxis1PlacementTransform(this IfcAxis1Placement ifcPlacement)
      {
         if (ifcPlacement == null)
            return Transform.Identity;

         Transform transform;
         if (IFCImportFile.TheFile.TransformMap.TryGetValue(ifcPlacement.StepId, out transform))
            return transform;

         IfcDirection ifcAxis = ifcPlacement.Axis;
         XYZ norm = ifcAxis == null ? XYZ.BasisZ : IFCPoint.ProcessNormalizedIFCDirection(ifcAxis);

         transform = ifcPlacement.GetPlacementTransform();
         Plane arbitraryPlane = Plane.CreateByNormalAndOrigin(norm, transform.Origin);

         transform.BasisX = arbitraryPlane.XVec;
         transform.BasisY = arbitraryPlane.YVec;
         transform.BasisZ = norm;

         IFCImportFile.TheFile.TransformMap[ifcPlacement.StepId] = transform;
         return transform;
      }

      /// <summary>
      /// Convert an IfcAxis2Placement into a transform.
      /// </summary>
      /// <param name="placement">The placement handle.</param>
      /// <returns>The transform.</returns>
      public static Transform GetAxis2PlacementTransform(this IfcAxis2Placement ifcPlacement)
      {
         if (ifcPlacement == null)
            return Transform.Identity;

         Transform transform;
         if (IFCImportFile.TheFile.TransformMap.TryGetValue(ifcPlacement.StepId, out transform))
            return transform;

         IfcAxis2Placement2D axis2Placement2D = ifcPlacement as IfcAxis2Placement2D;
         if (axis2Placement2D != null)
            transform = axis2Placement2D.GetAxis2Placement2DTransform();
         else
         {
            IfcAxis2Placement3D axis2Placement3D = ifcPlacement as IfcAxis2Placement3D;
            if (axis2Placement3D != null)
               transform = axis2Placement3D.GetAxis2Placement3DTransform();

            else
            {
               Importer.TheLog.LogUnhandledSubTypeError(ifcPlacement, IFCEntityType.IfcAxis2Placement3D, false);
               transform = Transform.Identity;
            }
         }

         IFCImportFile.TheFile.TransformMap[ifcPlacement.StepId] = transform;
         return transform;
      }

      /// <summary>
      /// Removes the relative transform for a site.
      /// </summary>
      public static void RemoveRelativeTransformForSite(IfcSite site)
      {
         if (site == null)
            return;
         IfcLocalPlacement localPlacement = site.ObjectPlacement as IfcLocalPlacement;
         if (localPlacement == null)
            return;

         localPlacement.RelativePlacement = site.Database.Factory.XYPlanePlacement;
      }
   }
}