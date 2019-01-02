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
using Revit.IFC.Import.Enums;
using Revit.IFC.Import.Geometry;
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   public static class IFCTrimmedCurve 
   {
      public static Curve CurveTrimmedCurve(this IfcTrimmedCurve trimmedCurve)
      {
         bool sameSense = trimmedCurve.SenseAgreement;
         IfcCurve ifcBasisCurve = trimmedCurve.BasisCurve;
         if (ifcBasisCurve == null)
         {
            // LOG: ERROR: Error processing BasisCurve # for IfcTrimmedCurve #.
            return null;
         }
         Curve basisCurve = ifcBasisCurve.Curve();
         if (basisCurve == null)
         {
            // LOG: ERROR: Expected a single curve, not a curve loop for BasisCurve # for IfcTrimmedCurve #.
            return null;
         }

         IfcTrimmingSelect trim1 = trimmedCurve.Trim1;
         if (trim1 == null)
         {
            // LOG: ERROR: Invalid data type for Trim1 attribute for IfcTrimmedCurve #.
            return null;
         }

         IfcTrimmingSelect trim2 = trimmedCurve.Trim2;
         if (trim2 == null)
         {
            // LOG: ERROR: Invalid data type for Trim1 attribute for IfcTrimmedCurve #.
            return null;
         }

         

         IfcTrimmingPreference trimPreference = trimmedCurve.MasterRepresentation;

         double param1 = 0.0, param2 = 0.0;
         try
         {
            trimmedCurve.GetTrimParameters(out param1, out param2);
         }
         catch (Exception ex)
         {
            Importer.TheLog.LogError(trimmedCurve.StepId, ex.Message, false);
            return null;
         }

         Curve baseCurve = ifcBasisCurve.Curve();
         if (baseCurve.IsCyclic)
         {
            if (!sameSense)
               MathUtil.Swap(ref param1, ref param2);

            if (param2 < param1)
               param2 = MathUtil.PutInRange(param2, param1 + Math.PI, 2 * Math.PI);

            if (param2 - param1 > 2.0 * Math.PI - MathUtil.Eps())
            {
               Importer.TheLog.LogWarning(trimmedCurve.StepId, "IfcTrimmedCurve length is greater than 2*PI, leaving unbound.", false);
               return baseCurve;
            }

            try
            {
               baseCurve.MakeBound(param1, param2);
            }
            catch (Exception ex)
            {
               if (ex.Message.Contains("too small"))
               {
                  Importer.TheLog.LogError(trimmedCurve.StepId, "curve length is invalid, ignoring.", false);
                  return null;
               }
               else
                  throw ex;
            }
         }
         else
         {
            if (MathUtil.IsAlmostEqual(param1, param2))
            {
               Importer.TheLog.LogError(trimmedCurve.StepId, "Param1 = Param2 for IfcTrimmedCurve #, ignoring.", false);
               return null;
            }

            if (param1 > param2 - MathUtil.Eps())
            {
               Importer.TheLog.LogWarning(trimmedCurve.StepId, "Param1 > Param2 for IfcTrimmedCurve #, reversing.", false);
               MathUtil.Swap(ref param1, ref param2);
            }

            Curve copyCurve = baseCurve.Clone();

            double length = param2 - param1;
            if (length <= IFCImportFile.TheFile.Document.Application.ShortCurveTolerance)
            {
               string lengthAsString = IFCUnitUtil.FormatLengthAsString(length);
               Importer.TheLog.LogError(trimmedCurve.StepId, "curve length of " + lengthAsString + " is invalid, ignoring.", false);
               return null;
            }

            copyCurve.MakeBound(param1, param2);
            if (sameSense)
            {
               return copyCurve;
            }
            else
            {
               return copyCurve.CreateReversed();
            }
         }
         return null;
      }

      private static void GetTrimParameters(this IfcTrimmedCurve trimmedCurve, out double param1, out double param2)
      {
         Curve basisCurve = trimmedCurve.BasisCurve.Curve();
         double? condParam1 = GetTrimParameter(trimmedCurve.Trim1, basisCurve, trimmedCurve.MasterRepresentation, false, trimmedCurve.StepId, trimmedCurve.Database);
         if (!condParam1.HasValue)
            throw new InvalidOperationException("#" + trimmedCurve.StepId + ": Couldn't apply first trimming parameter of IfcTrimmedCurve.");
         param1 = condParam1.Value;

         double? condParam2 = GetTrimParameter(trimmedCurve.Trim2, basisCurve, trimmedCurve.MasterRepresentation, false, trimmedCurve.StepId, trimmedCurve.Database);
         if (!condParam2.HasValue)
            throw new InvalidOperationException("#" + trimmedCurve.StepId + ": Couldn't apply second trimming parameter of IfcTrimmedCurve.");
         param2 = condParam2.Value;

         if (MathUtil.IsAlmostEqual(param1, param2))
         {  
            // If we had a cartesian parameter as the trim preference, check if the parameter values are better.
            if (trimmedCurve.MasterRepresentation == IfcTrimmingPreference.CARTESIAN)
            {
               condParam1 = GetTrimParameter(trimmedCurve.Trim1, basisCurve, IfcTrimmingPreference.PARAMETER, true, trimmedCurve.StepId, trimmedCurve.Database);
               if (!condParam1.HasValue)
                  throw new InvalidOperationException("#" + trimmedCurve.StepId + ": Couldn't apply first trimming parameter of IfcTrimmedCurve.");
               param1 = condParam1.Value;

               condParam2 = GetTrimParameter(trimmedCurve.Trim2, basisCurve, IfcTrimmingPreference.PARAMETER, true, trimmedCurve.StepId, trimmedCurve.Database);
               if (!condParam2.HasValue)
                  throw new InvalidOperationException("#" + trimmedCurve.StepId + ": Couldn't apply second trimming parameter of IfcTrimmedCurve.");
               param2 = condParam2.Value;
            }
            else
               throw new InvalidOperationException("#" + trimmedCurve.StepId + ": Ignoring 0 length curve.");
         }
      }

      

      private static double? GetTrimParameter(IfcTrimmingSelect trim, Curve basisCurve, IfcTrimmingPreference trimPreference, bool secondAttempt, int id, DatabaseIfc db)
      {
         bool preferParam = !(trimPreference == IfcTrimmingPreference.CARTESIAN);
         if (secondAttempt)
            preferParam = !preferParam;
         double vertexEps = IFCImportFile.TheFile.Document.Application.VertexTolerance;

         if (!preferParam)
         {
            IfcCartesianPoint cartesianPoint = db[trim.IfcCartesianPoint] as IfcCartesianPoint;
            if (cartesianPoint != null)
            {
               XYZ trimParamPt = IFCPoint.ProcessScaledLengthIFCCartesianPoint(cartesianPoint);
               if (trimParamPt == null)
               {
                  Importer.TheLog.LogWarning(id, "Invalid trim point for basis curve.", false);
               }
               else
               {
                  try
                  {
                     IntersectionResult result = basisCurve.Project(trimParamPt);
                     if (result.Distance < vertexEps)
                        return result.Parameter;

                     Importer.TheLog.LogWarning(id, "Cartesian value for trim point not on the basis curve.", false);
                  }
                  catch
                  {
                     Importer.TheLog.LogWarning(id, "Cartesian value for trim point not on the basis curve.", false);
                  }
               }
            }
         }
         else 
         {
            double trimParamDouble = trim.IfcParameterValue;
            if (!double.IsNaN(trimParamDouble))
            {
               if (basisCurve.IsCyclic)
                  trimParamDouble = IFCUnitUtil.ScaleAngle(trimParamDouble);
               else
                  trimParamDouble = IFCUnitUtil.ScaleLength(trimParamDouble);
               return trimParamDouble;
            }
         }

         // Try again with opposite preference.
         if (!secondAttempt)
            return GetTrimParameter(trim, basisCurve, trimPreference, true, id, db);

         return null;
      }
   }
}