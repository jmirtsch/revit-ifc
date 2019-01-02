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
   public static class IFCHalfSpaceSolid
   {
      /// <summary>
      /// Create geometry for an IfcHalfSpaceSolid.
      /// </summary>
      /// <param name="shapeEditScope">The shape edit scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      /// <returns>A list containing one geometry for the IfcHalfSpaceSolid.</returns>
      internal static IList<GeometryObject> CreateGeometryHalfSpacedSolid(this IfcHalfSpaceSolid halfSpaceSolid, CreateElementIfcCache cache,
            IFCImportShapeEditScope shapeEditScope, Transform unscaledLcs, Transform scaledLcs, string guid)
      {
         IfcPlane ifcPlane = halfSpaceSolid.BaseSurface as IfcPlane;
         Plane plane = ifcPlane.Plane();
         XYZ origin = plane.Origin;
         XYZ xVec = plane.XVec;
         XYZ yVec = plane.YVec;

         // Set some huge boundaries for now.
         const double largeCoordinateValue = 100000;
         XYZ[] corners = new XYZ[4] {
                unscaledLcs.OfPoint((xVec * -largeCoordinateValue) + (yVec * -largeCoordinateValue) + origin),
                unscaledLcs.OfPoint((xVec * largeCoordinateValue) + (yVec * -largeCoordinateValue) + origin),
                unscaledLcs.OfPoint((xVec * largeCoordinateValue) + (yVec * largeCoordinateValue) + origin),
                unscaledLcs.OfPoint((xVec * -largeCoordinateValue) + (yVec * largeCoordinateValue) + origin)
            };

         IList<CurveLoop> loops = new List<CurveLoop>();
         CurveLoop loop = new CurveLoop();
         for (int ii = 0; ii < 4; ii++)
         {
            if (halfSpaceSolid.AgreementFlag)
               loop.Append(Line.CreateBound(corners[(5 - ii) % 4], corners[(4 - ii) % 4]));
            else
               loop.Append(Line.CreateBound(corners[ii], corners[(ii + 1) % 4]));
         }
         loops.Add(loop);

         XYZ normal = unscaledLcs.OfVector(halfSpaceSolid.AgreementFlag ? -plane.Normal : plane.Normal);
         SolidOptions solidOptions = new SolidOptions(halfSpaceSolid.GetMaterialElementId(cache, shapeEditScope), shapeEditScope.GraphicsStyleId);
         Solid baseSolid = GeometryCreationUtilities.CreateExtrusionGeometry(loops, normal, largeCoordinateValue, solidOptions);

         IfcPolygonalBoundedHalfSpace polygonalBoundedHalfSpace = halfSpaceSolid as IfcPolygonalBoundedHalfSpace;
         if (polygonalBoundedHalfSpace != null)
         {
            IfcBoundedCurve boundedCurve = polygonalBoundedHalfSpace.PolygonalBoundary;
            if (boundedCurve != null)
            {
               CurveLoop polygonalBoundary = boundedCurve.GetCurveLoop();

               Transform unscaledTotalTransform = unscaledLcs.Multiply(polygonalBoundedHalfSpace.Position.GetAxis2Placement3DTransformUnscaled());
               Transform scaledTotalTransform = scaledLcs.Multiply(polygonalBoundedHalfSpace.Position.GetAxis2Placement3DTransform());

               // Make sure this bounding polygon extends below base of half-space soild.
               Transform moveBaseTransform = Transform.Identity;
               moveBaseTransform.Origin = new XYZ(0, 0, -largeCoordinateValue);

               unscaledTotalTransform = unscaledTotalTransform.Multiply(moveBaseTransform);
               scaledTotalTransform = scaledTotalTransform.Multiply(moveBaseTransform);

               CurveLoop transformedPolygonalBoundary = IFCGeometryUtil.CreateTransformed(polygonalBoundary, halfSpaceSolid.StepId, unscaledTotalTransform, scaledTotalTransform);
               IList<CurveLoop> boundingLoops = new List<CurveLoop>();
               boundingLoops.Add(transformedPolygonalBoundary);

               Solid boundingSolid = GeometryCreationUtilities.CreateExtrusionGeometry(boundingLoops, unscaledTotalTransform.BasisZ, 2.0 * largeCoordinateValue,
                   solidOptions);
               baseSolid = IFCGeometryUtil.ExecuteSafeBooleanOperation(halfSpaceSolid.StepId, boundedCurve.StepId, baseSolid, boundingSolid, BooleanOperationsType.Intersect, null);
            }
         }

         IList<GeometryObject> returnList = new List<GeometryObject>();
         returnList.Add(baseSolid);
         return returnList;
      }

      /// <summary>
      /// Return geometry for a particular representation item.
      /// </summary>
      /// <param name="shapeEditScope">The shape edit scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      /// <returns>The created geometries.</returns>
      internal static void CreateShapeHalfSpacedSolid(this IfcHalfSpaceSolid halfSpaceSolid, CreateElementIfcCache cache,
            IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         IList<GeometryObject> geometry = halfSpaceSolid.CreateGeometryHalfSpacedSolid(cache, shapeEditScope, lcs, scaledLcs, guid);
         if (geometry != null)
         {
            foreach (GeometryObject geom in geometry)
            {
               shapeEditScope.Solids.Add(IFCSolidInfo.Create(halfSpaceSolid.StepId, geom));
            }
         }
      }
   }
}