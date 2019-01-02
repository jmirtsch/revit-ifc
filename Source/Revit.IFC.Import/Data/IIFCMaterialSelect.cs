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
   /// Interface that contains shared functions for IfcMaterialSelect
   /// </summary>
   public static class IFCMaterialSelect
   {
      /// <summary>
      /// Return the material list for this IFCMaterialSelect.
      /// </summary>
      public static IList<IfcMaterial> GetMaterials(this IfcMaterialSelect materialSelect)
      {
         IfcMaterial material = materialSelect as IfcMaterial;
         if(material == null)
         {
            IfcMaterialConstituent materialConstituent = materialSelect as IfcMaterialConstituent;
            if (materialConstituent != null)
               material = materialConstituent.Material;
            else
            {
               IfcMaterialLayer materialLayer = materialSelect as IfcMaterialLayer;
               if (materialLayer != null)
                  material = materialLayer.Material;
               else
               {
                  IfcMaterialProfile materialProfile = materialSelect as IfcMaterialProfile;
                  if (materialProfile != null)
                     material = materialProfile.Material;
               }
            }
         }
         if (material != null)
            return new List<IfcMaterial>() { material };
         IfcMaterialConstituentSet materialConstituentSet = materialSelect as IfcMaterialConstituentSet;
         if (materialConstituentSet != null)
            return materialConstituentSet.GetMaterials();
         IfcMaterialLayerSet materialLayerSet = materialSelect as IfcMaterialLayerSet;
         if(materialLayerSet == null)
         {
            IfcMaterialLayerSetUsage materialLayerSetUsage = materialSelect as IfcMaterialLayerSetUsage;
            if (materialLayerSetUsage != null)
               materialLayerSet = materialLayerSetUsage.ForLayerSet;
         }
         if (materialLayerSet != null)
            return materialLayerSet.GetMaterials();
         IfcMaterialList materialList = materialSelect as IfcMaterialList;
         if (materialList != null)
            return materialList.Materials;
         IfcMaterialProfileSet materialProfileSet = materialSelect as IfcMaterialProfileSet;
         if(materialProfileSet == null)
         {
            IfcMaterialProfileSetUsage materialProfileSetUsage = materialSelect as IfcMaterialProfileSetUsage;
            if(materialProfileSetUsage != null)
            {
               IfcMaterialProfileSetUsageTapering materialProfileSetUsageTapering = materialSelect as IfcMaterialProfileSetUsageTapering;
               if(materialProfileSetUsageTapering != null)
               {
                  List<IfcMaterial> materials = new List<IfcMaterial>();
                  materials.AddRange(materialProfileSetUsageTapering.ForProfileSet.GetMaterials());
                  materials.AddRange(materialProfileSetUsageTapering.ForProfileEndSet.GetMaterials());
                  return materials;
               }
               materialProfileSet = materialProfileSetUsage.ForProfileSet;
            }
         }
         if (materialProfileSet != null)
            return materialProfileSet.GetMaterials();

         Importer.TheLog.LogUnhandledSubTypeError(materialSelect as BaseClassIfc, "IfcMaterialSelect", false);
         return null;
      }

      /// <summary>
      /// Create the elements associated with the IFCMaterialSelect.
      /// </summary>
      /// <param name="doc">The document.</param>
      internal static void Create(this IfcMaterialSelect materialSelect, CreateElementIfcCache cache)
      {
         IfcMaterial material = materialSelect as IfcMaterial;
         if (material == null)
         {
            IfcMaterialConstituent materialConstituent = materialSelect as IfcMaterialConstituent;
            if (materialConstituent != null)
               material = materialConstituent.Material;
            else
            {
               IfcMaterialLayer materialLayer = materialSelect as IfcMaterialLayer;
               if (materialLayer != null)
                  material = materialLayer.Material;
               else
               {
                  IfcMaterialProfile materialProfile = materialSelect as IfcMaterialProfile;
                  if (materialProfile != null)
                     material = materialProfile.Material;
               }
            }
         }
         if (material != null)
            material.Create(cache);
         else
         {
            IfcMaterialConstituentSet materialConstituentSet = materialSelect as IfcMaterialConstituentSet;
            if (materialConstituentSet != null)
               materialConstituentSet.Create(cache);
            else
            {
               IfcMaterialLayerSet materialLayerSet = materialSelect as IfcMaterialLayerSet;
               if(materialLayerSet == null)
               {
                  IfcMaterialLayerSetUsage materialLayerSetUsage = materialSelect as IfcMaterialLayerSetUsage;
                  if (materialLayerSetUsage != null)
                     materialLayerSet = materialLayerSetUsage.ForLayerSet;
               }
               if (materialLayerSet != null)
                  materialLayerSet.Create(cache);
               else
               {
                  IfcMaterialList materialList = materialSelect as IfcMaterialList;
                  if(materialList != null)
                  {
                     foreach (IfcMaterial m in materialList.Materials)
                        m.Create(cache);
                  }
                  else
                  {
                     IfcMaterialProfileSet materialProfileSet = materialSelect as IfcMaterialProfileSet;
                     if(materialProfileSet == null)
                     {
                        IfcMaterialProfileSetUsage materialProfileSetUsage = materialSelect as IfcMaterialProfileSetUsage;
                        if(materialProfileSetUsage != null)
                        {
                           IfcMaterialProfileSetUsageTapering materialProfileSetUsageTapering = materialProfileSetUsage as IfcMaterialProfileSetUsageTapering;
                           if (materialProfileSetUsageTapering != null)
                              materialProfileSetUsageTapering.ForProfileEndSet.Create(cache);
                           materialProfileSet = materialProfileSetUsage.ForProfileSet;
                        }
                     }
                     if (materialProfileSet != null)
                        materialProfileSet.Create(cache);
                     else
                     {
                        Importer.TheLog.LogUnhandledSubTypeError(materialSelect as BaseClassIfc, "IfcMaterialSelect", false);
                     }
                        
                  }
               }
            }
         }

      }
   }
}