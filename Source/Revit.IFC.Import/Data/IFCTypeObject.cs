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
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   /// <summary>
   /// Represents an IfcTypeObject.
   /// </summary>
   public static class IFCTypeObject
   {
      private static HashSet<IFCEntityType> m_sNoPredefinedTypePreIFC4 = null;

      private static bool HasPredefinedType(IFCEntityType type)
      {
         if (IFCImportFile.TheFile.SchemaVersion < ReleaseVersion.IFC4)
         {
            // Note that this is just a list of entity types that are dealt with generically; 
            // other types may override the base function.
            if (m_sNoPredefinedTypePreIFC4 == null)
            {
               m_sNoPredefinedTypePreIFC4 = new HashSet<IFCEntityType>();
               m_sNoPredefinedTypePreIFC4.Add(IFCEntityType.IfcDiscreteAccessoryType);
               m_sNoPredefinedTypePreIFC4.Add(IFCEntityType.IfcDistributionElementType);
               m_sNoPredefinedTypePreIFC4.Add(IFCEntityType.IfcDoorStyle);
               m_sNoPredefinedTypePreIFC4.Add(IFCEntityType.IfcFastenerType);
               m_sNoPredefinedTypePreIFC4.Add(IFCEntityType.IfcFurnishingElementType);
               m_sNoPredefinedTypePreIFC4.Add(IFCEntityType.IfcFurnitureType);
               m_sNoPredefinedTypePreIFC4.Add(IFCEntityType.IfcWindowStyle);
            }

            if (m_sNoPredefinedTypePreIFC4.Contains(type))
               return false;
         }

         return true;
      }

      /// <summary>
      /// Creates or populates Revit elements based on the information contained in this class.
      /// </summary>
      /// <param name="doc">The document.</param>
      internal static void CreateType(this IfcTypeObject typeObject, CreateElementIfcCache cache)
      {
         DirectShapeType shapeType = Importer.TheCache.UseElementByGUID<DirectShapeType>(cache.Document, typeObject.GlobalId);

         if (shapeType == null)
         {
            shapeType = IFCElementUtil.CreateElementType(cache.Document, typeObject.GetVisibleName(), typeObject.CategoryId(cache), typeObject.StepId);
         }
         else
         {
            // If we used the element from the cache, we want to make sure that the IFCRepresentationMap can access it
            // instead of creating a new element.
            Importer.TheCache.CreatedDirectShapeTypes[typeObject.StepId] = shapeType.Id;
         }

         if (shapeType == null)
            throw new InvalidOperationException("Couldn't create DirectShapeType for IfcTypeObject.");

         cache.CreatedElements[typeObject.StepId] = shapeType.Id;
      }
   }
}