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
   public static class IFCBooleanResult 
   {
      /// <summary>
      /// Get the styled item corresponding to the solid inside of an IFCRepresentationItem.
      /// </summary>
      /// <param name="repItem">The representation item.</param>
      /// <returns>The corresponding IFCStyledItem, or null if not found.</returns>
      /// <remarks>This function is intended to work on an IFCBooleanResult with an arbitrary number of embedded
      /// clipping operations.  We will take the first StyledItem that corresponds to either an IFCBooleanResult,
      /// or the contained solid.  We explicitly do not want any material associated specifically with the void.</remarks>

      private static IfcStyledItem GetStyledItemFromOperand(IfcRepresentationItem repItem)
      {
         if (repItem == null)
            return null;

         if (repItem.StyledByItem != null)
            return repItem.StyledByItem;

         IfcBooleanResult booleanResult = repItem as IfcBooleanResult;
         if (booleanResult != null)
         {
            IfcRepresentationItem firstOperand = booleanResult.FirstOperand as IfcRepresentationItem; 
            if (firstOperand != null)
               return GetStyledItemFromOperand(firstOperand);
         }

         return null;
      }

      /// <summary>
      /// Return geometry for a particular representation item.
      /// </summary>
      /// <param name="shapeEditScope">The geometry creation scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      /// <returns>The created geometry.</returns>
      public static IList<GeometryObject> CreateGeometryBooleanResult(this IfcBooleanResult booleanResult, CreateElementIfcCache cache,
            IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         IList<GeometryObject> firstSolids = booleanResult.FirstOperand.CreateGeometryBooleanOperand(cache, shapeEditScope, lcs, scaledLcs, guid);

         if (firstSolids != null)
         {
            foreach (GeometryObject potentialSolid in firstSolids)
            {
               if (!(potentialSolid is Solid))
               {
                  Importer.TheLog.LogError(booleanResult.FirstOperand.StepId, "Can't perform Boolean operation on a Mesh.", false);
                  return firstSolids;
               }
            }
         }

         IList<GeometryObject> secondSolids = null;
         IfcBooleanOperand secondOperand = booleanResult.SecondOperand;
         if ((firstSolids != null || booleanResult.Operator == IfcBooleanOperator.UNION) && (secondOperand != null))
         {
            try
            {
               using (IFCImportShapeEditScope.BuildPreferenceSetter setter =
                   new IFCImportShapeEditScope.BuildPreferenceSetter(shapeEditScope, IFCImportShapeEditScope.BuildPreferenceType.ForceSolid))
               {
                  // Before we process the second operand, we are going to see if there is a uniform material set for the first operand 
                  // (corresponding to the solid in the Boolean operation).  We will try to suggest the same material for the voids to avoid arbitrary
                  // setting of material information for the cut faces.
                  IfcStyledItem firstOperandStyledItem = GetStyledItemFromOperand(booleanResult.FirstOperand as IfcRepresentationItem);
                  using (IFCImportShapeEditScope.IFCMaterialStack stack =
                      new IFCImportShapeEditScope.IFCMaterialStack(shapeEditScope, cache, firstOperandStyledItem, null))
                  {
                     secondSolids = secondOperand.CreateGeometryBooleanOperand(cache, shapeEditScope, lcs, scaledLcs, guid);
                  }
               }
            }
            catch (Exception ex)
            {
               // We will allow something to be imported, in the case where the second operand is invalid.
               // If the first (base) operand is invalid, we will still fail the import of this solid.
               if (secondOperand is IfcRepresentationItem)
                  Importer.TheLog.LogError(secondOperand.StepId, ex.Message, false);
               else
                  throw ex;
               secondSolids = null;
            }
         }

         IList<GeometryObject> resultSolids = null;
         if (firstSolids == null)
         {
            if (booleanResult.Operator == IfcBooleanOperator.UNION)
               resultSolids = secondSolids;
         }
         else if (secondSolids == null)
         {
            resultSolids = firstSolids;
         }
         else
         {
            BooleanOperationsType booleanOperationsType = BooleanOperationsType.Difference;
            switch (booleanResult.Operator)
            {
               case IfcBooleanOperator.DIFFERENCE:
                  booleanOperationsType = BooleanOperationsType.Difference;
                  break;
               case IfcBooleanOperator.INTERSECTION:
                  booleanOperationsType = BooleanOperationsType.Intersect;
                  break;
               case IfcBooleanOperator.UNION:
                  booleanOperationsType = BooleanOperationsType.Union;
                  break;
               default:
                  Importer.TheLog.LogError(booleanResult.StepId, "Invalid BooleanOperationsType.", true);
                  break;
            }

            resultSolids = new List<GeometryObject>();
            foreach (GeometryObject firstSolid in firstSolids)
            {
               Solid resultSolid = (firstSolid as Solid);

               int secondId = (secondOperand == null) ? -1 : secondOperand.StepId;
               XYZ suggestedShiftDirection = (secondOperand == null) ? null : secondOperand.GetSuggestedShiftDirection(lcs);
               foreach (GeometryObject secondSolid in secondSolids)
               {
                  resultSolid = IFCGeometryUtil.ExecuteSafeBooleanOperation(booleanResult.StepId, secondId, resultSolid, secondSolid as Solid, booleanOperationsType, suggestedShiftDirection);
                  if (resultSolid == null)
                     break;
               }

               if (resultSolid != null)
                  resultSolids.Add(resultSolid);
            }
         }

         return resultSolids;
      }

      /// <summary>
      /// Create geometry for a particular representation item, and add to scope.
      /// </summary>
      /// <param name="shapeEditScope">The geometry creation scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      public static void CreateShapeBooleanResult(this IfcBooleanResult booleanResult, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         IList<GeometryObject> resultGeometries = booleanResult.CreateGeometryBooleanResult(cache, shapeEditScope, lcs, scaledLcs, guid);
         if (resultGeometries != null)
         {
            foreach (GeometryObject resultGeometry in resultGeometries)
            {
               shapeEditScope.Solids.Add(IFCSolidInfo.Create(booleanResult.StepId, resultGeometry));
            }
         }
      }
   }
}