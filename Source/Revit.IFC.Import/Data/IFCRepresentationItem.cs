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
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   public static class IFCRepresentationItem
   {
      /// <summary>
      /// Returns the associated material id, if any.
      /// </summary>
      /// <param name="scope">The containing creation scope.</param>
      /// <returns>The element id of the material, if any.</returns>
      public static ElementId GetMaterialElementId(this IfcRepresentationItem representationItem, CreateElementIfcCache cache, IFCImportShapeEditScope scope)
      {
         ElementId materialId = scope.GetCurrentMaterialId();
         if (materialId != ElementId.InvalidElementId)
            return materialId;

         if (scope.Creator != null)
         {
            IfcMaterial creatorMaterial = scope.Creator.GetTheMaterial();
            if (creatorMaterial != null && cache.CreatedElements.TryGetValue(creatorMaterial.StepId, out materialId))
               return materialId;
         }

         return ElementId.InvalidElementId;
      }

      internal static IfcPresentationLayerAssignment SetShapeEdit(this IfcRepresentationItem representationItem, IFCImportShapeEditScope shapeEditScope, CreateElementIfcCache cache)
      {
         IfcStyledItem styledByItem = representationItem.StyledByItem;
         if (styledByItem != null)
            styledByItem.Create(shapeEditScope, cache);

         IfcPresentationLayerAssignment presentationLayerAssignment = representationItem.LayerAssignment.FirstOrDefault();
         if (presentationLayerAssignment != null)
            presentationLayerAssignment.CreatePresentationLayerAssignment(shapeEditScope, cache);
         return presentationLayerAssignment;
      }
      /// <summary>
      /// Create geometry for a particular representation item.
      /// </summary>
      /// <param name="shapeEditScope">The geometry creation scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      public static void CreateShape(this IfcRepresentationItem representationItem, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         IfcPresentationLayerAssignment presentationLayerAssignment = representationItem.SetShapeEdit(shapeEditScope, cache);

         using (IFCImportShapeEditScope.IFCMaterialStack stack = new IFCImportShapeEditScope.IFCMaterialStack(shapeEditScope, cache, representationItem.StyledByItem, presentationLayerAssignment))
         {
           representationItem.CreateShapeItem(cache, shapeEditScope, lcs, scaledLcs, guid);
         }
      }

      /// <summary>
      /// Create geometry for a particular representation item.
      /// </summary>
      /// <param name="shapeEditScope">The geometry creation scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      internal static void CreateShapeItem(this IfcRepresentationItem representationItem, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         IfcGeometricRepresentationItem geometricRepresentationItem = representationItem as IfcGeometricRepresentationItem;
         if (geometricRepresentationItem != null)
         {
            IfcSolidModel solidModel = representationItem as IfcSolidModel;
            if (solidModel != null)
               solidModel.CreateShapeSolidModel(cache, shapeEditScope, lcs, scaledLcs, guid);
            else
            {
               IfcBooleanResult booleanResult = representationItem as IfcBooleanResult;
               if (booleanResult != null)
                  booleanResult.CreateShapeBooleanResult(cache, shapeEditScope, lcs, scaledLcs, guid);
               else
               {
                  IfcConnectedFaceSet connectedFaceSet = representationItem as IfcConnectedFaceSet;
                  if (connectedFaceSet != null)
                     connectedFaceSet.CreateShapeConnectedFaceSet(cache, shapeEditScope, lcs, scaledLcs, guid, true);
                  else
                  {
                     IfcCurve curve = representationItem as IfcCurve;
                     if (curve != null)
                        curve.CreateShapeCurve(cache, shapeEditScope, lcs, scaledLcs, guid);
                     else
                     {
                        IfcGeometricSet geometricSet = representationItem as IfcGeometricSet;
                        if (geometricSet != null)
                           geometricSet.CreateShapeGeometricSet(cache, shapeEditScope, lcs, scaledLcs, guid);
                        else
                        {

                           IfcManifoldSolidBrep manifoldSolidBrep = representationItem as IfcManifoldSolidBrep;
                           if (manifoldSolidBrep != null)
                              manifoldSolidBrep.CreateShapeManifoldSolidBrep(cache, shapeEditScope, lcs, scaledLcs, guid);
                           else
                           {

                              IfcShellBasedSurfaceModel shellBasedSurfaceModel = representationItem as IfcShellBasedSurfaceModel;
                              if (shellBasedSurfaceModel != null)
                              {
                                 shellBasedSurfaceModel.CreateShapeShellBasedSurfaceModel(cache, shapeEditScope, lcs, scaledLcs, guid);
                              }
                              else
                              {
                                 IfcTriangulatedFaceSet triangulatedFaceSet = representationItem as IfcTriangulatedFaceSet;
                                 if (triangulatedFaceSet != null)
                                    triangulatedFaceSet.CreateShapeTriangulatedFaceSet(cache, shapeEditScope, lcs, scaledLcs, guid);
                                 else
                                 {
                                    Importer.TheLog.LogUnhandledSubTypeError(representationItem, IFCEntityType.IfcRepresentationItem, true);
                                 }
                              }
                           }
                        }

                     }
                  }
               }
            }
            
         }
         else
         {
            IfcMappedItem mappedItem = representationItem as IfcMappedItem;
            if(mappedItem != null)
            {
               mappedItem.CreateShapeMappedItem(cache, shapeEditScope, lcs, scaledLcs, guid);
            }
            else
            {
               IfcTopologicalRepresentationItem topologicalRepresentationItem = representationItem as IfcTopologicalRepresentationItem;
               if(topologicalRepresentationItem != null)
               {
                  IfcFace face = topologicalRepresentationItem as IfcFace;
                  if (face != null)
                      face.CreateShapeFace(cache, shapeEditScope, lcs, scaledLcs, guid);
                  else
                  {
                     IfcFaceBound faceBound = topologicalRepresentationItem as IfcFaceBound;
                     if (faceBound != null)
                        faceBound.CreateShapeFaceBound(cache, shapeEditScope, lcs, scaledLcs, guid);
                  }
               }
            }
         }
      }
   }
}