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
   public static class IFCSurfaceOfLinearExtrusion
   {
      /// <summary>
      /// Get the local surface transform at a given point on the surface.
      /// </summary>
      /// <param name="pointOnSurface">The point.</param>
      /// <returns>The transform.</returns>
      /// <remarks>This does not include the translation component.</remarks>
      public static Transform GetTransformAtPoint(this IfcSurfaceOfLinearExtrusion surfaceOfLinearExtrusion, XYZ pointOnSurface, CreateElementIfcCache cache)
      {
         IFCSimpleProfile simpleProfile = IFCSimpleProfile.CreateIFCSimpleProfile(surfaceOfLinearExtrusion.SweptCurve, cache);
         if (simpleProfile == null)
         {
            // LOG: ERROR: warn that we only support simple profiles.
            return null;
         }

         CurveLoop outerCurveLoop = simpleProfile.OuterCurve;
         if (outerCurveLoop == null || outerCurveLoop.Count() != 1)
         {
            // LOG: ERROR
            return null;
         }

         Curve outerCurve = outerCurveLoop.First();
         if (outerCurve == null)
         {
            // LOG: ERROR
            return null;
         }

         IntersectionResult result = outerCurve.Project(pointOnSurface);
         if (result == null)
         {
            // LOG: ERROR
            return null;
         }

         double parameter = result.Parameter;

         Transform atPoint = outerCurve.ComputeDerivatives(parameter, false);
         atPoint.set_Basis(0, atPoint.BasisX.Normalize());
         atPoint.set_Basis(1, atPoint.BasisY.Normalize());
         atPoint.set_Basis(2, atPoint.BasisZ.Normalize());
         atPoint.Origin = pointOnSurface;

         return atPoint;
      }
      /// <summary>
      /// Returns the surface which defines the internal shape of the face
      /// </summary>
      /// <param name="lcs">The local coordinate system for the surface.  Can be null.</param>
      /// <returns>The surface which defines the internal shape of the face</returns>
      public static Surface GetSurface(this IfcSurfaceOfLinearExtrusion surfaceOfLinearExtrusion, Transform lcs, CreateElementIfcCache cache)
      {
         Curve sweptCurve = null;
         IFCSimpleProfile simpleProfile = IFCSimpleProfile.CreateIFCSimpleProfile(surfaceOfLinearExtrusion.SweptCurve, cache);
         // Get the RuledSurface which is used to create the geometry from the brepbuilder
         if (simpleProfile == null)
         {
            return null;
         }
         else
         {
            // Currently there is no easy way to get the curve from the IFCProfile, so for now we assume that
            // the SweptCurve is an IFCSimpleProfile and its outer curve only contains one curve, which is the 
            // profile curve that we want
            CurveLoop outerCurve = simpleProfile.OuterCurve;
            if (outerCurve == null)
            {
               return null;
            }
            CurveLoopIterator it = outerCurve.GetCurveLoopIterator();
            sweptCurve = it.Current;
         }

         // Create the second profile curve by translating the first one in the extrusion direction
         Curve profileCurve2 = sweptCurve.CreateTransformed(Transform.CreateTranslation(surfaceOfLinearExtrusion.ExtrudedDirection.ProcessNormalizedIFCDirection().Multiply(IFCUnitUtil.ScaleLength(surfaceOfLinearExtrusion.Depth))));

         if (lcs == null)
            return RuledSurface.Create(sweptCurve, profileCurve2);

         Curve transformedProfileCurve1 = sweptCurve.CreateTransformed(lcs);
         Curve transformedProfileCurve2 = profileCurve2.CreateTransformed(lcs);

         return RuledSurface.Create(transformedProfileCurve1, transformedProfileCurve2);
      }
   }
}