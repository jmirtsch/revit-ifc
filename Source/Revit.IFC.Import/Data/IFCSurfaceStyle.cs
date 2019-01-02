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
   public static class IFCSurfaceStyle
   {

      /// <summary>
      /// Create the material associated to the element, and return the id.
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="forcedName">An optional name that sets the name of the material created, regardless of surface style name.</param>
      /// <param name="suggestedName">An optional name that suggests the name of the material created, if the surface style name is null.</param>
      /// <param name="idOverride">The id of the parent item, used if forcedName is used.</param>
      /// <returns>The material id.</returns>
      /// <remarks>If forcedName is not null, this will not store the created element id in this class.</remarks>
      public static ElementId CreateSurfaceStyle(this IfcSurfaceStyle surfaceStyle, CreateElementIfcCache cache, string forcedName, string suggestedName, int idOverride)
      {
         try
         {
            bool overrideName = (forcedName != null) && (string.Compare(forcedName, surfaceStyle.Name) != 0);
            ElementId result = ElementId.InvalidElementId;
            if (!overrideName && cache.CreatedElements.TryGetValue(surfaceStyle.StepId, out result))
               return result;

            string name = overrideName ? forcedName : surfaceStyle.Name;
            if (string.IsNullOrEmpty(name))
            {
               if (!string.IsNullOrEmpty(suggestedName))
                  name = suggestedName;
               else
                  name = "IFC Surface Style";
            }
            int id = overrideName ? idOverride : surfaceStyle.StepId;
            if(!cache.InvalidForCreation.Contains(id))
            {
               Color color = null;
               int? transparency = null;
               int? shininess = null;
               int? smoothness = null;

               IfcSurfaceStyleShading shading = surfaceStyle.Styles.OfType<IfcSurfaceStyleShading>().FirstOrDefault();
               if (shading != null)
               {
                  color = shading.SurfaceColour.CreateColor();
                  IfcSurfaceStyleRendering rendering = shading as IfcSurfaceStyleRendering;
                  if (rendering != null)
                  {
                     transparency = (int)(rendering.Transparency * 100 + 0.5);
                     shininess = rendering.GetShininess();
                     smoothness = rendering.GetSmoothness();
                  }
               }

               IFCMaterialInfo materialInfo =
                   IFCMaterialInfo.Create(color, transparency, shininess, smoothness, ElementId.InvalidElementId);
               ElementId createdElementId = IFCMaterial.CreateMaterialElem(cache, id, name, materialInfo);
               if (!overrideName)
                  cache.CreatedElements[surfaceStyle.StepId] = createdElementId;
               return createdElementId;
            }
         }
         catch (Exception ex)
         {
            cache.InvalidForCreation.Add(surfaceStyle.StepId);
            Importer.TheLog.LogCreationError(surfaceStyle, ex.Message, false);
         }

         return ElementId.InvalidElementId;
      }

      
   }
}