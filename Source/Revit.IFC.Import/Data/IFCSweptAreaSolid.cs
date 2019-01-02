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
   public static class IFCSweptAreaSolid 
   {
      private static IList<CurveLoop> GetTransformedCurveLoopsFromSimpleProfile(this IfcSweptAreaSolid sweptAreaSolid, IFCSimpleProfile simpleSweptArea, Transform unscaledLcs, Transform scaledLcs)
      {
         IList<CurveLoop> loops = new List<CurveLoop>();

         // It is legal for simpleSweptArea.Position to be null, for example for IfcArbitraryClosedProfileDef.
         Transform unscaledSweptAreaPosition =
             (simpleSweptArea.Position == null) ? unscaledLcs : unscaledLcs.Multiply(simpleSweptArea.Position);

         Transform scaledSweptAreaPosition =
             (simpleSweptArea.Position == null) ? scaledLcs : scaledLcs.Multiply(simpleSweptArea.Position);

         CurveLoop currLoop = simpleSweptArea.OuterCurve;
         if (currLoop == null || currLoop.Count() == 0)
         {
            Importer.TheLog.LogError(sweptAreaSolid.StepId, "No outer curve loop for profile, ignoring.", false);
            return null;
         }
         loops.Add(IFCGeometryUtil.CreateTransformed(currLoop, sweptAreaSolid.StepId, unscaledSweptAreaPosition, scaledSweptAreaPosition));

         if (simpleSweptArea.InnerCurves != null)
         {
            foreach (CurveLoop innerCurveLoop in simpleSweptArea.InnerCurves)
               loops.Add(IFCGeometryUtil.CreateTransformed(innerCurveLoop, sweptAreaSolid.StepId, unscaledSweptAreaPosition, scaledSweptAreaPosition));
         }

         return loops;
      }

      private static void GetTransformedCurveLoopsFromProfile(this IfcSweptAreaSolid sweptAreaSolid, IfcProfileDef profile, Transform unscaledLcs, Transform scaledLcs, ISet<IList<CurveLoop>> loops, CreateElementIfcCache cache)
      {
         IFCSimpleProfile simpleProfile = IFCSimpleProfile.CreateIFCSimpleProfile(profile, cache);
         if (simpleProfile != null)
         {
            IList<CurveLoop> currLoops = GetTransformedCurveLoopsFromSimpleProfile(sweptAreaSolid, simpleProfile, unscaledLcs, scaledLcs);
            if (currLoops != null && currLoops.Count > 0)
               loops.Add(currLoops);
         }
         else
         {
            IfcCompositeProfileDef compositeProfileDef = profile as IfcCompositeProfileDef;
            if (compositeProfileDef != null)
            {
               foreach (IfcProfileDef subProfile in compositeProfileDef.Profiles)
                  GetTransformedCurveLoopsFromProfile(sweptAreaSolid, subProfile, unscaledLcs, scaledLcs, loops, cache);
            }
            else
            {
               IfcDerivedProfileDef derivedProfileDef = profile as IfcDerivedProfileDef;
               if (derivedProfileDef != null)
               {

                  Transform fullUnscaledLCS = unscaledLcs;
                  Transform localLCS = derivedProfileDef.Operator.GetTransform();
                  if (fullUnscaledLCS == null)
                     fullUnscaledLCS = localLCS;
                  else if (localLCS != null)
                     fullUnscaledLCS = fullUnscaledLCS.Multiply(localLCS);

                  Transform fullScaledLCS = scaledLcs;
                  if (fullScaledLCS == null)
                     fullScaledLCS = localLCS;
                  else if (localLCS != null)
                     fullScaledLCS = fullScaledLCS.Multiply(localLCS);

                  GetTransformedCurveLoopsFromProfile(sweptAreaSolid, derivedProfileDef.ContainerProfile, fullUnscaledLCS, fullScaledLCS, loops, cache);
               }
               else
               {
                  // TODO: Support.
                  Importer.TheLog.LogError(sweptAreaSolid.StepId, "SweptArea Profile #" + profile.StepId + " not yet supported.", false);
               }
            }
         }
      }

      /// <summary>
      /// Gathers a set of transformed curve loops.  Each member of the set has exactly one outer and zero of more inner loops.
      /// </summary>
      /// <param name="lcs">The unscaled transform, if the scaled transform isn't supported.</param>
      /// <param name="lcs">The scaled (true) transform.</param>
      /// <returns>The set of list of curveloops representing logically disjoint profiles of exactly one outer and zero of more inner loops.</returns>
      /// <remarks>We state "logically disjoint" because the code does not check the validity of the loops at this time.</remarks>
      internal static ISet<IList<CurveLoop>> GetTransformedCurveLoops(this IfcSweptAreaSolid sweptAreaSolid, Transform lcs, Transform scaledLCS, CreateElementIfcCache cache)
      {
         ISet<IList<CurveLoop>> loops = new HashSet<IList<CurveLoop>>();
         GetTransformedCurveLoopsFromProfile(sweptAreaSolid, sweptAreaSolid.SweptArea, lcs, scaledLCS, loops, cache);
         return loops;
      }
   }
}