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
using Revit.IFC.Import.Enums;
using Revit.IFC.Import.Geometry;
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   public static class IFCCurve
   {

      /// <summary>
      /// Get the Curve representation of IFCCurve.  It could be null.
      /// </summary>
      public static Curve Curve(this IfcCurve curve)
      {
         if (curve == null)
         {
            Importer.TheLog.LogNullError(IFCEntityType.IfcCurve);
            return null;
         }
         IfcBoundedCurve boundedCurve = curve as IfcBoundedCurve;
         if (boundedCurve != null)
            return boundedCurve.Curve();
         IfcConic conic = curve as IfcConic;
         if (conic != null)
            return conic.Curve();
         IfcLine line = curve as IfcLine;
         if (line != null)
            return line.Line();
         IfcOffsetCurve2D offsetCurve2D = curve as IfcOffsetCurve2D;
         if (offsetCurve2D != null)
            return offsetCurve2D.Curve();
         IfcOffsetCurve3D offsetCurve3D = curve as IfcOffsetCurve3D;
         if (offsetCurve3D != null)
            return offsetCurve3D.Curve();
         return null;
      }
      public static CurveLoop CurveLoop(this IfcCurve curve)
      {
         if (curve == null)
         {
            Importer.TheLog.LogNullError(IFCEntityType.IfcCurve);
            return null;
         }
         
         IfcOffsetCurve2D offsetCurve2D = curve as IfcOffsetCurve2D;
         if (offsetCurve2D != null)
            return offsetCurve2D.CurveLoop();
         IfcOffsetCurve3D offsetCurve3D = curve as IfcOffsetCurve3D;
         if (offsetCurve3D != null)
            return offsetCurve3D.CurveLoop();
         return null;
      }
     
      /// <summary>
      /// Get the CurveLoop representation of IFCCurve.  It could be null.
      /// </summary>
      public static CurveLoop GetCurveLoop(this IfcCurve ifcCurve)
      {
         Curve curve = ifcCurve.Curve();
         if(curve == null)
            return null;
         CurveLoop curveLoop = new CurveLoop();
         curveLoop.Append(curve);
         return curveLoop;
      }

      /// <summary>
      /// Get the curve or CurveLoop representation of IFCCurve, as a list of 0 or more curves.
      /// </summary>
      public static IList<Curve> GetCurves(this IfcCurve ifcCurve)
      {
         Curve curve = ifcCurve.Curve();
         if (curve != null)
            return new List<Curve>() { curve };
         List<Curve> curves = new List<Curve>();
         CurveLoop curveLoop = ifcCurve.CurveLoop();
         if(curveLoop != null)
         {
            foreach (Curve c in curveLoop)
               curves.Add(c);
         }

         return curves;
      }

      /// <summary>
      /// Calculates the normal of the plane of the curve or curve loop.
      /// </summary>
      /// <returns>The normal, or null if there is no curve or curve loop.</returns>
      public static XYZ GetNormal(this IfcCurve ifcCurve)
      {
         Curve curve = ifcCurve.Curve();
         if(curve != null)
         {
            Transform transform = curve.ComputeDerivatives(0, false);
            if (transform != null)
               return transform.BasisZ;
         }
         CurveLoop curveLoop = ifcCurve.CurveLoop();
         if (curveLoop != null)
         {
            try
            {
               Plane plane = curveLoop.GetPlane();
               if (plane != null)
                  return plane.Normal;
            }
            catch
            {
            }
         }

         return null;
      }

      

      

      private static Curve CreateTransformedCurve(Curve baseCurve, IfcRepresentation parentRep, Transform lcs, int id)
      {
         Curve transformedCurve = (baseCurve != null) ? baseCurve.CreateTransformed(lcs) : null;
         if (transformedCurve == null)
         {
            Importer.TheLog.LogWarning(id, "couldn't create curve for " + ((parentRep == null) ? "" : parentRep.RepresentationIdentifier.ToString()) + " representation.", false);
         }

         return transformedCurve;
      }

      /// <summary>
      /// Create geometry for a particular representation item, and add to scope.
      /// </summary>
      /// <param name="shapeEditScope">The geometry creation scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      internal static void CreateShapeCurve(this IfcCurve ifcCurve, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         IfcRepresentation parentRep = shapeEditScope.ContainingRepresentation;

         IList<Curve> transformedCurves = new List<Curve>();
         Curve curve = ifcCurve.Curve();
         if (curve != null)
         {
            Curve transformedCurve = CreateTransformedCurve(curve, parentRep, lcs, ifcCurve.StepId);
            if (transformedCurve != null)
               transformedCurves.Add(transformedCurve);
         }
         else
         {
            CurveLoop curveLoop = ifcCurve.CurveLoop();
            if (curveLoop != null)
            {
               foreach (Curve c in curveLoop)
               {
                  Curve transformedCurve = CreateTransformedCurve(c, parentRep, lcs, ifcCurve.StepId);
                  if (transformedCurve != null)
                     transformedCurves.Add(transformedCurve);
               }
            }
         }

         // TODO: set graphics style for footprint curves.
         IFCRepresentationIdentifier repId = IFCRepresentationIdentifier.Unhandled;
         if (parentRep == null || !Enum.TryParse<IFCRepresentationIdentifier>(parentRep.RepresentationIdentifier, out repId))
            repId = IFCRepresentationIdentifier.Unhandled;
         bool createModelGeometry = (repId == IFCRepresentationIdentifier.Body) || (repId == IFCRepresentationIdentifier.Axis) || (repId == IFCRepresentationIdentifier.Unhandled);

         ElementId gstyleId = ElementId.InvalidElementId;
         if (createModelGeometry)
         {
            Category curveCategory = IFCCategoryUtil.GetSubCategoryForRepresentation(cache, ifcCurve.StepId, repId);
            if (curveCategory != null)
            {
               GraphicsStyle graphicsStyle = curveCategory.GetGraphicsStyle(GraphicsStyleType.Projection);
               if (graphicsStyle != null)
                  gstyleId = graphicsStyle.Id;
            }
         }

         foreach (Curve c in transformedCurves)
         {
            if (createModelGeometry)
            {
               curve.SetGraphicsStyleId(gstyleId);
               shapeEditScope.Solids.Add(IFCSolidInfo.Create(ifcCurve.StepId, c));
            }
            else
            {
               // Default: assume a plan view curve.
               shapeEditScope.FootPrintCurves.Add(c);
            }
         }
      }
   }
}