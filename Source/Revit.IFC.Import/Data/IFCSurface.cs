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
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Revit.IFC.Common.Utility;
using Revit.IFC.Common.Enums;
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   /// <summary>
   /// Class that represents IFCSurface entity
   /// </summary>
   public static class IFCSurface
   {
      /// <summary>
      /// Get the local surface transform at a given point on the surface.
      /// </summary>
      /// <param name="pointOnSurface">The point.</param>
      /// <returns>The transform.</returns>
      public static Transform GetTransformAtPoint(this IfcSurface surface, XYZ pointOnSurface, CreateElementIfcCache cache)
      {
         IfcElementarySurface elementarySurface = surface as IfcElementarySurface;
         if(elementarySurface != null)
         {
            Transform position = new Transform(elementarySurface.Position.GetAxis2Placement3DTransform());
            position.Origin = pointOnSurface;
            return position;
         }
         IfcSurfaceOfLinearExtrusion surfaceOfLinearExtrusion = surface as IfcSurfaceOfLinearExtrusion;
         if (surfaceOfLinearExtrusion != null)
            return surfaceOfLinearExtrusion.GetTransformAtPoint(pointOnSurface, cache);
         return null;
      }

      /// <summary>
      /// Returns the surface which defines the internal shape of the face
      /// </summary>
      /// <param name="lcs">The local coordinate system for the surface.  Can be null.</param>
      /// <returns>The surface which defines the internal shape of the face</returns>
      public static Surface GetSurface(this IfcSurface surface, Transform lcs)
      {
         if(surface is IfcBSplineSurface)
            throw new InvalidOperationException("Revit doesn't have corresponding surface type for NURBS");
         IfcElementarySurface elementarySurface = surface as IfcElementarySurface;
         if (elementarySurface != null)
         {
            Transform transform = elementarySurface.Position.GetAxis2Placement3DTransform();
            XYZ origin = transform.Origin;
            XYZ xVec = transform.BasisX;
            XYZ yVec = transform.BasisY;
            XYZ zVec = transform.BasisZ;
            IfcCylindricalSurface cylindricalSurface = surface as IfcCylindricalSurface;
            if (cylindricalSurface != null)
               return CylindricalSurface.Create(new Frame(lcs.OfPoint(origin), lcs.OfVector(xVec), lcs.OfVector(yVec), lcs.OfVector(zVec)), IFCUnitUtil.ScaleLength(cylindricalSurface.Radius));
            IfcPlane plane = elementarySurface as IfcPlane;
            if(plane != null)
               return Plane.CreateByNormalAndOrigin(lcs.OfVector(zVec), lcs.OfPoint(origin));
         }
         IfcSurfaceOfLinearExtrusion surfaceOfLinearExtrusion = surface as IfcSurfaceOfLinearExtrusion;
         if (surfaceOfLinearExtrusion != null)
            return surfaceOfLinearExtrusion.GetSurface(lcs);
         return null;
      }
   }
}