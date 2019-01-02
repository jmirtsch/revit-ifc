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
   /// <summary>
   /// Class that represents IFCSurfaceOfRevolution entity
   /// </summary>
   public static class IFCSurfaceOfRevolution 
   {
      /// <summary>
      /// Returns the surface which defines the internal shape of the face
      /// </summary>
      /// <param name="lcs">The local coordinate system for the surface.  Can be null.</param>
      /// <returns>The surface which defines the internal shape of the face</returns>
      public static Surface GetSurface(this IfcSurfaceOfRevolution surfaceOfRevolution, Transform lcs, CreateElementIfcCache cache)
      {
         IfcProfileDef sweptCurve = surfaceOfRevolution.SweptCurve;
         if (sweptCurve == null)
            Importer.TheLog.LogError(surfaceOfRevolution.StepId, "Cannot find the profile curve of this revolved face.", true);

         IFCSimpleProfile simpleProfile = IFCSimpleProfile.CreateIFCSimpleProfile(sweptCurve, cache);
         if (simpleProfile == null)
            Importer.TheLog.LogError(surfaceOfRevolution.StepId, "Can't handle profile curve of type " + sweptCurve.StepClassName + ".", true);

         CurveLoop outerCurve = simpleProfile.OuterCurve;
         Curve profileCurve = (outerCurve != null) ? outerCurve.First<Curve>() : null;

         if (profileCurve == null)
            Importer.TheLog.LogError(surfaceOfRevolution.StepId, "Cannot create the profile curve of this revolved surface.", true);

         if (outerCurve.Count() > 1)
            Importer.TheLog.LogError(surfaceOfRevolution.StepId, "Revolved surface has multiple profile curves, ignoring all but first.", false);

         Curve revolvedSurfaceProfileCurve = profileCurve.CreateTransformed(surfaceOfRevolution.Position.GetAxis2Placement3DTransform());
         Transform axisPosition = surfaceOfRevolution.AxisPosition.GetIFCAxis1PlacementTransform();
         if (!RevolvedSurface.IsValidProfileCurve(axisPosition.Origin, axisPosition.BasisZ, revolvedSurfaceProfileCurve))
            Importer.TheLog.LogError(surfaceOfRevolution.StepId, "Profile curve is invalid for this revolved surface.", true);

         if (lcs == null)
            return RevolvedSurface.Create(axisPosition.Origin, axisPosition.BasisZ, revolvedSurfaceProfileCurve);

         Curve transformedRevolvedSurfaceProfileCurve = revolvedSurfaceProfileCurve.CreateTransformed(lcs);
         return RevolvedSurface.Create(lcs.OfPoint(axisPosition.Origin), lcs.OfVector(axisPosition.BasisZ), transformedRevolvedSurfaceProfileCurve);
      }
   }
}