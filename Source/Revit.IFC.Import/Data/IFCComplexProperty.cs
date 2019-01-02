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
using Autodesk.Revit.DB.IFC;
using Revit.IFC.Common.Utility;
using Revit.IFC.Common.Enums;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   /// <summary>
   /// Represents an IfcComplexProperty.
   /// </summary>-
   public static class IFCComplexProperty
   {
      /// <summary>
      /// Returns the property value as a string, for SetValueString().
      /// </summary>
      /// <returns>The property value as a string.</returns>
      public static string PropertyValueAsString(this IfcComplexProperty complexProperty)
      {
         int numValues = complexProperty.HasProperties.Count;
         if (numValues == 0)
            return "";

         string propertyValue = "";
         foreach (IfcProperty property in complexProperty.HasProperties.Values)
         {
            if (propertyValue != "")
               propertyValue += "; ";
            propertyValue += property.Name + ": " + property.PropertyValueAsString();
         }

         return propertyValue;
      }
   }
}