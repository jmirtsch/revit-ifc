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
   public static class IFCPresentationLayerAssignment 
   {
      /// <summary>
      /// Create the Revit elements associated with this IfcPresentationLayerAssignment.
      /// </summary>
      /// <param name="shapeEditScope">The shape edit scope.</param>
      public static void CreatePresentationLayerAssignment(this IfcPresentationLayerAssignment presentationLayerAssignment, IFCImportShapeEditScope shapeEditScope, CreateElementIfcCache cache)
      {
         if (!string.IsNullOrWhiteSpace(presentationLayerAssignment.Name))
            shapeEditScope.PresentationLayerNames.Add(presentationLayerAssignment.Name);

         IfcPresentationLayerWithStyle presentationLayerWithStyle = presentationLayerAssignment as IfcPresentationLayerWithStyle;
         if(presentationLayerWithStyle != null)
         {
            // TODO: support cut pattern id and cut pattern color.
            if(cache.CreatedElements.ContainsKey(presentationLayerWithStyle.StepId) || cache.InvalidForCreation.Contains(presentationLayerWithStyle.StepId))
               return;

            try
            {
               // If the styled item or the surface style has a name, use it.
               IfcSurfaceStyle surfaceStyle = presentationLayerWithStyle.LayerStyles.OfType<IfcSurfaceStyle>().FirstOrDefault();
               if (surfaceStyle == null)
               {
                  // We only handle surface styles at the moment; log file should already reflect any other unhandled styles.
                  cache.CreatedElements.Add(presentationLayerWithStyle.StepId, ElementId.InvalidElementId);
                  return;
               }

               string forcedName = surfaceStyle.Name;
               if (string.IsNullOrWhiteSpace(forcedName))
                  forcedName = presentationLayerAssignment.Name;

               surfaceStyle.CreateSurfaceStyle(cache, forcedName, null, presentationLayerAssignment.StepId);
            }
            catch (Exception ex)
            {
               cache.InvalidForCreation.Add(presentationLayerAssignment.StepId);
               Importer.TheLog.LogCreationError(presentationLayerAssignment, ex.Message, false);
            }
         }
      }
      public static ElementId GetMaterialElementId(this IfcPresentationLayerAssignment presentationLayerAssignment, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope)
      {
         ElementId elementId;
         if (cache.CreatedElements.TryGetValue(presentationLayerAssignment.StepId, out elementId))
            return elementId;
         IfcPresentationLayerWithStyle presentationLayerWithStyle = presentationLayerAssignment as IfcPresentationLayerWithStyle;
         if (presentationLayerWithStyle != null)
         {
            IfcSurfaceStyle surfaceStyle = presentationLayerWithStyle.LayerStyles.OfType<IfcSurfaceStyle>().FirstOrDefault();
            if (surfaceStyle != null)
               return surfaceStyle.CreateSurfaceStyle(cache, string.IsNullOrEmpty(surfaceStyle.Name) ? presentationLayerAssignment.Name : "", null, presentationLayerAssignment.StepId);

         }
         return cache.CreatedElements[presentationLayerAssignment.StepId] = ElementId.InvalidElementId;
      }
      /// <summary>
      /// Does a top-level check to see if this entity may be equivalent to otherEntity.
      /// </summary>
      /// <param name="otherEntity">The other IFCEntity.</param>
      /// <returns>True if they are equivalent, false if they aren't, null if not enough information.</returns>
      /// <remarks>This isn't intended to be an exhaustive check, and isn't implemented for all types.  This is intended
      /// to be used by derived classes.</remarks>
      public static bool MaybeEquivalentTo(this IfcPresentationLayerAssignment entity, IfcPresentationLayerAssignment other)
      {
         if (other == null)
            return false;

         if (!IFCNamingUtil.SafeStringsAreEqual(entity.Name, other.Name))
            return false;

         if (!IFCNamingUtil.SafeStringsAreEqual(entity.Description, other.Description))
            return false;

         if (!IFCEntity.AreIFCEntityListsEquivalent(entity.AssignedItems, other.AssignedItems))
            return false;

         if (!IFCNamingUtil.SafeStringsAreEqual(entity.Identifier, other.Identifier))
            return false;

         IfcPresentationLayerWithStyle presentationLayerWithStyle = entity as IfcPresentationLayerWithStyle;
         if (presentationLayerWithStyle != null)
         {
            IfcPresentationLayerWithStyle otherPresentationLayerWithStyle = other as IfcPresentationLayerWithStyle;
            if (otherPresentationLayerWithStyle == null)
               return false;
            if (!IFCEntity.AreIFCEntityListsEquivalent(presentationLayerWithStyle.LayerStyles, otherPresentationLayerWithStyle.LayerStyles))
               return false;
         }

         return true;
      }
      static public bool CheckLayerAssignmentConsistency(this IfcPresentationLayerAssignment originalAssignment,
          IfcPresentationLayerAssignment layerAssignment, int id)
      {
         if ((originalAssignment != null) && (!originalAssignment.MaybeEquivalentTo(layerAssignment)))
         {
            Importer.TheLog.LogWarning(id, "Multiple inconsistent layer assignment items found for this item; using first one.", false);
            return false;
         }

         return true;
      }
   }
}