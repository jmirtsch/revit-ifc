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
using Revit.IFC.Common.Enums;
using Revit.IFC.Common.Utility;
using Revit.IFC.Import.Enums;
using Revit.IFC.Import.Geometry;
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   /// <summary>
   /// Represents an IfcRepresentationMap.
   /// </summary>
   public static class IFCRepresentationMap
   {
      /// <summary>
      /// Create geometry for a particular representation map.
      /// </summary>
      /// <param name="shapeEditScope">The geometry creation scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <remarks>For this function, if lcs is null, we will create a library item for the geometry.</remarks>
      public static void CreateShapeRepresentationMap(this IfcRepresentationMap representationMap, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         bool creatingLibraryDefinition = (lcs == null);

         if (representationMap.MappedRepresentation != null)
         {
            // Look for cached shape; if found, return.
            if (creatingLibraryDefinition)
            {
               if (IFCImportFile.TheFile.ShapeLibrary.FindDefinitionType(representationMap.StepId.ToString()) != ElementId.InvalidElementId)
                  return;
            }

            Transform mappingTransform = null;
            if (lcs == null)
               mappingTransform = representationMap.MappingOrigin.GetAxis2PlacementTransform();
            else
            {
               if (representationMap.MappingOrigin == null)
                  mappingTransform = lcs;
               else
                  mappingTransform = lcs.Multiply(representationMap.MappingOrigin.GetAxis2PlacementTransform());
            }

            Transform scaledMappingTransform = null;
            if (scaledLcs == null)
               scaledMappingTransform = mappingTransform;
            else
            {
               if (representationMap.MappingOrigin == null)
                  scaledMappingTransform = scaledLcs;
               else
                  scaledMappingTransform = scaledLcs.Multiply(representationMap.MappingOrigin.GetAxis2PlacementTransform());
            }

            int numExistingSolids = shapeEditScope.Solids.Count;
            int numExistingCurves = shapeEditScope.FootPrintCurves.Count;

            representationMap.MappedRepresentation.CreateShape(cache, shapeEditScope, mappingTransform, scaledMappingTransform, guid);

            if (creatingLibraryDefinition)
            {
               int numNewSolids = shapeEditScope.Solids.Count;
               int numNewCurves = shapeEditScope.FootPrintCurves.Count;

               if ((numExistingSolids != numNewSolids) || (numExistingCurves != numNewCurves))
               {
                  IList<GeometryObject> mappedSolids = new List<GeometryObject>();
                  for (int ii = numExistingSolids; ii < numNewSolids; ii++)
                  {
                     mappedSolids.Add(shapeEditScope.Solids[numExistingSolids].GeometryObject);
                     shapeEditScope.Solids.RemoveAt(numExistingSolids);
                  }

                  IList<Curve> mappedCurves = new List<Curve>();
                  for (int ii = numExistingCurves; ii < numNewCurves; ii++)
                  {
                     mappedCurves.Add(shapeEditScope.FootPrintCurves[numExistingCurves]);
                     shapeEditScope.FootPrintCurves.RemoveAt(numExistingCurves);
                  }
                  shapeEditScope.AddPlaneViewCurves(mappedCurves, representationMap.StepId);

                  Document doc = IFCImportFile.TheFile.Document;
                  DirectShapeType directShapeType = null;

                  if(representationMap.Represents.Count == 1)
                  {
                     IfcTypeProduct typeProduct = representationMap.Represents[0]; 
                     ElementId directShapeTypeId = ElementId.InvalidElementId;
                     if (Importer.TheCache.CreatedDirectShapeTypes.TryGetValue(typeProduct.StepId, out directShapeTypeId))
                     {
                        directShapeType = doc.GetElement(directShapeTypeId) as DirectShapeType;
                     }
                  }

                  if (directShapeType == null)
                  {
                     string directShapeTypeName = representationMap.StepId.ToString();
                     directShapeType = IFCElementUtil.CreateElementType(doc, directShapeTypeName, shapeEditScope.CategoryId, representationMap.StepId);
                  }

                  // Note that this assumes that there is only one 2D rep per DirectShapeType.
                  directShapeType.AppendShape(mappedSolids);
                  if (mappedCurves.Count != 0)
                     shapeEditScope.SetPlanViewRep(directShapeType);

                  IFCImportFile.TheFile.ShapeLibrary.AddDefinitionType(representationMap.StepId.ToString(), directShapeType.Id);
               }
            }
         }
      }

     
   }
}