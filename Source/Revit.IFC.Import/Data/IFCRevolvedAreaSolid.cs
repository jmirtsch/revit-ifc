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
   public static class IFCRevolvedAreaSolid 
   {
      private static XYZ GetValidXVectorFromLoop(CurveLoop curveLoop, XYZ zVec, XYZ origin)
      {
         foreach (Curve curve in curveLoop)
         {
            IList<XYZ> pointsToCheck = new List<XYZ>();

            // If unbound, must be cyclic.
            if (!curve.IsBound)
            {
               pointsToCheck.Add(curve.Evaluate(0, false));
               pointsToCheck.Add(curve.Evaluate(Math.PI / 2.0, false));
               pointsToCheck.Add(curve.Evaluate(Math.PI, false));
            }
            else
            {
               pointsToCheck.Add(curve.Evaluate(0, true));
               pointsToCheck.Add(curve.Evaluate(1.0, true));
               if (curve.IsCyclic)
                  pointsToCheck.Add(curve.Evaluate(0.5, true));
            }

            foreach (XYZ pointToCheck in pointsToCheck)
            {
               XYZ possibleVec = (pointToCheck - origin);
               XYZ yVec = zVec.CrossProduct(possibleVec).Normalize();
               if (yVec.IsZeroLength())
                  continue;
               return yVec.CrossProduct(zVec);
            }
         }

         return null;
      }

      /// <summary>
      /// Return geometry for a particular representation item.
      /// </summary>
      /// <param name="shapeEditScope">The shape edit scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      /// <returns>One or more created Solids.</returns>
      internal static IList<GeometryObject> CreateGeometryRevolvedAreaSolid(this IfcRevolvedAreaSolid revolvedAreaSolid, CreateElementIfcCache cache,
            IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         Transform origLCS = (lcs == null) ? Transform.Identity : lcs;
         IfcAxis2Placement3D position = revolvedAreaSolid.Position;
         Transform unscaledRevolvePosition = position == null ? origLCS : origLCS.Multiply(position.GetPlacementTransformUnscaled());

         Transform scaledOrigLCS = (scaledLcs == null) ? Transform.Identity : scaledLcs;
         Transform scaledRevolvePosition = (position == null) ? scaledOrigLCS : scaledOrigLCS.Multiply(position.GetPlacementTransform());

         ISet<IList<CurveLoop>> disjointLoops = revolvedAreaSolid.GetTransformedCurveLoops(unscaledRevolvePosition, scaledRevolvePosition, cache);
         if (disjointLoops == null || disjointLoops.Count() == 0)
            return null;

         Transform axis = revolvedAreaSolid.Axis.GetIFCAxis1PlacementTransform();
         XYZ frameOrigin = scaledRevolvePosition.OfPoint(axis.Origin);
         XYZ frameZVec = scaledRevolvePosition.OfVector(axis.BasisZ);
         SolidOptions solidOptions = new SolidOptions(revolvedAreaSolid.GetMaterialElementId(cache, shapeEditScope), shapeEditScope.GraphicsStyleId);

         IList<GeometryObject> myObjs = new List<GeometryObject>();

         foreach (IList<CurveLoop> loops in disjointLoops)
         {
            XYZ frameXVec = null;

            frameXVec = GetValidXVectorFromLoop(loops[0], frameZVec, frameOrigin);
            if (frameXVec == null)
            {
               Importer.TheLog.LogError(revolvedAreaSolid.StepId, "Couldn't generate valid frame for IfcRevolvedAreaSolid.", false);
               return null;
            }
            XYZ frameYVec = frameZVec.CrossProduct(frameXVec);
            Frame coordinateFrame = new Frame(frameOrigin, frameXVec, frameYVec, frameZVec);

            GeometryObject myObj = GeometryCreationUtilities.CreateRevolvedGeometry(coordinateFrame, loops, 0, IFCUnitUtil.ScaleAngle(revolvedAreaSolid.Angle), solidOptions);
            if (myObj != null)
               myObjs.Add(myObj);
         }

         return myObjs;
      }

      /// <summary>
      /// Create geometry for a particular representation item.
      /// </summary>
      /// <param name="shapeEditScope">The geometry creation scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      internal static void CreateShapeRevolvedAreaSolid(this IfcRevolvedAreaSolid revolvedAreaSolid, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         IList<GeometryObject> revolvedGeometries = revolvedAreaSolid.CreateGeometryRevolvedAreaSolid(cache, shapeEditScope, lcs, scaledLcs, guid);
         if (revolvedGeometries != null)
         {
            foreach (GeometryObject revolvedGeometry in revolvedGeometries)
            {
               shapeEditScope.Solids.Add(IFCSolidInfo.Create(revolvedAreaSolid.StepId, revolvedGeometry));
            }
         }
      }
   }
}