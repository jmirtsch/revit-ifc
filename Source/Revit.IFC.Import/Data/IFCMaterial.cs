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
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Revit.IFC.Common.Enums;
using Revit.IFC.Common.Utility;
using Revit.IFC.Import.Enums;
using Revit.IFC.Import.Utility;
using Revit.IFC.Import.Properties;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   /// <summary>
   /// Class to represent materials in IFC files.
   /// </summary>
   public static class IFCMaterial
   {
      private static string GetMaterialName(int id, string originalName)
      {
         // Disallow creating multiple materials with the same name.  This means that the
         // same material, with different styles, will be created with different names.
         string materialName = Importer.TheCache.CreatedMaterials.GetUniqueMaterialName(originalName, id);

         string revitMaterialName = IFCNamingUtil.CleanIFCName(materialName);
         if (revitMaterialName != null)
            return revitMaterialName;

         return String.Format(Resources.IFCDefaultMaterialName, id);
      }

      /// <summary>
      /// Create a Revit Material.
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="id">The id of the IFCEntity, used to avoid creating duplicate material names.</param>
      /// <param name="originalName">The base name of the material.</param>
      /// <param name="materialInfo">The material information.</param>
      /// <returns>The element id.</returns>
      public static ElementId CreateMaterialElem(CreateElementIfcCache cache, int id, string originalName, IFCMaterialInfo materialInfo)
      {
         ElementId createdElementId = Importer.TheCache.CreatedMaterials.FindMatchingMaterial(originalName, id, materialInfo);
         if (createdElementId != ElementId.InvalidElementId)
            return createdElementId;

         string revitMaterialName = GetMaterialName(id, originalName);

         createdElementId = Material.Create(cache.Document, revitMaterialName);
         if (createdElementId == ElementId.InvalidElementId)
            return createdElementId;

         materialInfo.ElementId = createdElementId;
         Importer.TheCache.CreatedMaterials.Add(originalName, materialInfo);

         // Get info.
         Material materialElem = cache.Document.GetElement(createdElementId) as Material;
         if (materialElem == null)
            return ElementId.InvalidElementId;

         bool materialHasValidColor = false;

         // We don't want an invalid value set below to prevent creating an element; log the message and move on.
         try
         {
            if (materialInfo.Color != null)
            {
               materialElem.Color = materialInfo.Color;
               materialHasValidColor = true;
            }
            else
            {
               materialElem.Color = new Color(127, 127, 127);
            }

            if (materialInfo.Transparency.HasValue)
               materialElem.Transparency = materialInfo.Transparency.Value;

            if (materialInfo.Shininess.HasValue)
               materialElem.Shininess = materialInfo.Shininess.Value;

            if (materialInfo.Smoothness.HasValue)
               materialElem.Smoothness = materialInfo.Smoothness.Value;
         }
         catch (Exception ex)
         {
            Importer.TheLog.LogError(id, "Couldn't set some Material values: " + ex.Message, false);
         }

         if (!materialHasValidColor)
            Importer.TheCache.MaterialsWithNoColor.Add(createdElementId);

         if (Importer.TheOptions.VerboseLogging)
         {
            string comment = "Created Material: " + revitMaterialName
                + " with color: (" + materialElem.Color.Red + ", " + materialElem.Color.Green + ", " + materialElem.Color.Blue + ") "
                + "transparency: " + materialElem.Transparency + " shininess: " + materialElem.Shininess + " smoothness: " + materialElem.Smoothness;
            Importer.TheLog.LogComment(id, comment, false);
         }

         Importer.TheLog.AddCreatedMaterial(cache.Document, createdElementId);
         cache.CreatedElements.Add(id, createdElementId);
         return createdElementId;
      }

      /// <summary>
      /// Traverse through the MaterialDefinitionRepresentation to get the style information relevant to the material.
      /// </summary>
      /// <returns>The one IFCSurfaceStyle.</returns>
      private static IfcSurfaceStyle GetSurfaceStyle(this IfcMaterial material)
      {
         IfcMaterialDefinitionRepresentation materialDefinitionRepresentation = material.HasRepresentation;
         if(materialDefinitionRepresentation != null)
            return materialDefinitionRepresentation.GetSurfaceStyle();

         return null;
      }

      /// <summary>
      /// Creates a Revit material based on the information contained in this class.
      /// </summary>
      /// <param name="doc">The document.</param>
      public static ElementId Create(this IfcMaterial material, CreateElementIfcCache cache)
      {
         // TODO: support cut pattern id and cut pattern color.
         try
         {
            string name = material.Name;
            if (string.IsNullOrEmpty(name))
               name = String.Format(Resources.IFCDefaultMaterialName, material.StepId);

            ElementId elementId;
            if (cache.CreatedElements.TryGetValue(material.StepId, out elementId))
               return elementId;
            if (cache.InvalidForCreation.Contains(material.StepId))
               return ElementId.InvalidElementId;
            IfcSurfaceStyle surfaceStyle = material.GetSurfaceStyle();
            if (surfaceStyle != null)
               return surfaceStyle.CreateSurfaceStyle(cache, name, null, material.StepId);
            else
            {
               IFCMaterialInfo materialInfo = IFCMaterialInfo.Create(null, null, null, null, ElementId.InvalidElementId);
               return CreateMaterialElem(cache, material.StepId, material.Name, materialInfo);
            }
         }
         catch (Exception ex)
         {
            cache.InvalidForCreation.Add(material.StepId);
            Importer.TheLog.LogCreationError(material, ex.Message, false);
         }
         return ElementId.InvalidElementId;
      }
   }
}