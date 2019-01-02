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
   public static class IFCStyledItem
   {
      /// <summary>
      /// Does a top-level check to see if this styled item may be equivalent to another styled item.
      /// </summary>
      /// <param name="otherEntity">The other styled item.</param>
      /// <returns>False if they don't have the same handles, null otherwise.</returns>
      public static bool? MaybeEquivalentTo(this IfcStyledItem styledItem, IfcStyledItem otherEntity)
      {
         if (otherEntity == null)
            return false;
         IfcStyleAssignmentSelect style1 = styledItem.Styles.FirstOrDefault(), style2 = otherEntity.Styles.FirstOrDefault();
         if (!IFCRoot.Equals(style1, style2))
            return false;

         return null;
      }

      /// <summary>
      /// Get the IFCSurfaceStyle associated with this IFCStyledItem.
      /// </summary>
      /// <returns>The IFCSurfaceStyle, if any.</returns>
      public static IfcSurfaceStyle GetSurfaceStyle(this IfcStyledItem item)
      {
         IfcSurfaceStyle surfaceStyle = null;
         foreach (IfcStyleAssignmentSelect style in item.Styles)
         {
            surfaceStyle = style as IfcSurfaceStyle;
            if (style != null)
               return surfaceStyle;
            IfcPresentationStyleAssignment styleAssignment = style as IfcPresentationStyleAssignment;
            if (styleAssignment != null)
            {
               foreach (IfcPresentationStyle presentationStyle in styleAssignment.Styles)
               {
                  surfaceStyle = presentationStyle as IfcSurfaceStyle;
                  if (surfaceStyle != null)
                     return surfaceStyle;
               }
            }
         }
         return null;
      }

      /// <summary>
      /// Creates a Revit material based on the information contained in this class.
      /// </summary>
      /// <param name="doc">The document.</param>
      public static void Create(this IfcStyledItem styledItem, IFCImportShapeEditScope shapeEditScope, CreateElementIfcCache cache)
      {

         // TODO: support cut pattern id and cut pattern color.
         if (cache.InvalidForCreation.Contains(styledItem.StepId))
            return;

         try
         {
            // If the styled item or the surface style has a name, use it.
            IfcSurfaceStyle surfaceStyle = styledItem.GetSurfaceStyle();
            if (surfaceStyle == null)
            {
               // We only handle surface styles at the moment; log file should already reflect any other unhandled styles.
               cache.InvalidForCreation.Add(styledItem.StepId); 
               return;
            }

            string forcedName = surfaceStyle.Name;
            if (string.IsNullOrEmpty(forcedName))
               forcedName = styledItem.Name;

            string suggestedName = null;
            if (styledItem.Item != null)
            {
               IfcObjectDefinition creator = shapeEditScope.Creator;
               IfcMaterial material = creator.GetTheMaterial();
               if(material != null)
                  suggestedName = material.Name;
            }

            cache.CreatedElements[styledItem.StepId] = surfaceStyle.CreateSurfaceStyle(cache, forcedName, suggestedName, styledItem.StepId);
         }
         catch (Exception ex)
         {
            cache.InvalidForCreation.Add(styledItem.StepId);
            Importer.TheLog.LogCreationError(styledItem, ex.Message, false);
         }
      }

     
   }
}