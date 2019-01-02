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
   public static class IFCSurfaceCurveSweptAreaSolid
   {
      /// <summary>
      /// Return geometry for a particular representation item.
      /// </summary>
      /// <param name="shapeEditScope">The geometry creation scope.</param>
      /// <param name="unscaledLcs">The unscaled local coordinate system for the geometry, if the scaled version isn't supported downstream.</param>
      /// <param name="scaledLcs">The scaled (true) local coordinate system for the geometry.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      /// <returns>The created geometry.</returns>
      internal static IList<GeometryObject> CreateGeometrySurfaceCurveSweptAreaSolid(this IfcSurfaceCurveSweptAreaSolid surfaceCurveSweptAreaSolid, CreateElementIfcCache cache,
            IFCImportShapeEditScope shapeEditScope, Transform unscaledLcs, Transform scaledLcs, string guid)
      {
         Transform unscaledObjectPosition = (unscaledLcs == null) ? surfaceCurveSweptAreaSolid.Position.GetAxis2Placement3DTransformUnscaled() : unscaledLcs.Multiply(surfaceCurveSweptAreaSolid.Position.GetAxis2Placement3DTransformUnscaled());
         Transform scaledObjectPosition = (scaledLcs == null) ? surfaceCurveSweptAreaSolid.Position.GetAxis2Placement3DTransform() : scaledLcs.Multiply(surfaceCurveSweptAreaSolid.Position.GetAxis2PlacementTransform());

         CurveLoop trimmedDirectrix = IFCGeometryUtil.TrimCurveLoop(surfaceCurveSweptAreaSolid.StepId, surfaceCurveSweptAreaSolid.Directrix, surfaceCurveSweptAreaSolid.StartParam, surfaceCurveSweptAreaSolid.EndParam);
         if (trimmedDirectrix == null)
            return null;

         double startParam = 0.0; // If the directrix isn't bound, this arbitrary parameter will do.
         Transform originTrf0 = null;
         Curve firstCurve0 = trimmedDirectrix.First();
         if (firstCurve0.IsBound)
            startParam = firstCurve0.GetEndParameter(0);
         originTrf0 = firstCurve0.ComputeDerivatives(startParam, false);
         if (originTrf0 == null)
            return null;

         // Note: the computation of the reference Surface Local Transform must be done before the directrix is transform to LCS (because the ref surface isn't)
         //     and therefore the origin is at the start of the curve should be the start of the directrix that lies on the surface.
         //     This is needed to transform the swept area that must be perpendicular to the start of the directrix curve

         Transform referenceSurfaceLocalTransform = surfaceCurveSweptAreaSolid.ReferenceSurface.GetTransformAtPoint(originTrf0.Origin, cache);

         CurveLoop trimmedDirectrixInLCS = IFCGeometryUtil.CreateTransformed(trimmedDirectrix, surfaceCurveSweptAreaSolid.StepId, unscaledObjectPosition, scaledObjectPosition);

         // Create the sweep.
         Transform originTrf = null;
         Curve firstCurve = trimmedDirectrixInLCS.First();
         //if (firstCurve.IsBound)
         //    startParam = firstCurve.GetEndParameter(0);
         originTrf = firstCurve.ComputeDerivatives(startParam, false);

         Transform unscaledReferenceSurfaceTransform = unscaledObjectPosition.Multiply(referenceSurfaceLocalTransform);
         Transform scaledReferenceSurfaceTransform = scaledObjectPosition.Multiply(referenceSurfaceLocalTransform);

         Transform profileCurveLoopsTransform = Transform.CreateTranslation(originTrf.Origin);
         profileCurveLoopsTransform.BasisX = scaledReferenceSurfaceTransform.BasisZ;
         profileCurveLoopsTransform.BasisZ = originTrf.BasisX.Normalize();
         profileCurveLoopsTransform.BasisY = profileCurveLoopsTransform.BasisZ.CrossProduct(profileCurveLoopsTransform.BasisX);

         ISet<IList<CurveLoop>> profileCurveLoops = surfaceCurveSweptAreaSolid.GetTransformedCurveLoops(profileCurveLoopsTransform, profileCurveLoopsTransform, cache);
         if (profileCurveLoops == null || profileCurveLoops.Count == 0)
            return null;

         SolidOptions solidOptions = new SolidOptions(surfaceCurveSweptAreaSolid.GetMaterialElementId(cache, shapeEditScope), shapeEditScope.GraphicsStyleId);
         IList<GeometryObject> myObjs = new List<GeometryObject>();
         foreach (IList<CurveLoop> loops in profileCurveLoops)
         {
            GeometryObject myObj = GeometryCreationUtilities.CreateSweptGeometry(trimmedDirectrixInLCS, 0, startParam, loops, solidOptions);
            if (myObj != null)
               myObjs.Add(myObj);
         }

         return myObjs;
      }
   }
}