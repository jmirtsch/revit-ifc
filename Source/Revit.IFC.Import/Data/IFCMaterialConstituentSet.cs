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
using System.Linq;
using System.Collections.Generic;
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
   /// Class representing IFCMaterialConstituentSet
   /// </summary>
   public static class IFCMaterialConstituentSet 
   {
      public static void Create(this IfcMaterialConstituentSet materialConstituentSet, CreateElementIfcCache cache)
      {
         foreach (IfcMaterialConstituent materialConstituent in materialConstituentSet.MaterialConstituents)
            materialConstituent.Create(cache);
      }

      /// <summary>
      /// Get the list of associated Materials
      /// </summary>
      /// <returns></returns>
      public static IList<IfcMaterial> GetMaterials(this IfcMaterialConstituentSet materialConstituentSet)
      {
         HashSet<IfcMaterial> materials = new HashSet<IfcMaterial>();
         foreach (IfcMaterialConstituent materialConstituent in materialConstituentSet.MaterialConstituents)
         {
            IList<IfcMaterial> constituentMaterials = materialConstituent.GetMaterials();
            foreach (IfcMaterial material in constituentMaterials)
               materials.Add(material);
         }
         return materials.ToList();
      }
   }
}