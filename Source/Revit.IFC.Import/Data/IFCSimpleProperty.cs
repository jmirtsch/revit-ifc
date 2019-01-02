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
   /// Represents an IfcSimpleProperty
   /// </summary>
   public static class IFCSimpleProperty
   {
      internal static IFCUnit GetUnit(this IfcSimpleProperty simpleProperty)
      {
         IfcUnit unit = null;
         IfcPropertySingleValue propertySingleValue = simpleProperty as IfcPropertySingleValue;
         if(propertySingleValue != null)
            unit = propertySingleValue.Unit;
         else
         {
            IfcPropertyBoundedValue propertyBoundedValue = simpleProperty as IfcPropertyBoundedValue;
            if (propertyBoundedValue != null)
               unit = propertyBoundedValue.Unit;
         }

         if(unit == null)
            return null;
         return new IFCUnit(unit);
      }
      internal static IList<IfcValue> GetPropertyValues(this IfcSimpleProperty simpleProperty)
      {
         IfcPropertySingleValue propertySingleValue = simpleProperty as IfcPropertySingleValue;
         if(propertySingleValue != null)
         {
            return new List<IfcValue>() { propertySingleValue.NominalValue };
         }
         IfcPropertyEnumeratedValue propertyEnumeratedValue = simpleProperty as IfcPropertyEnumeratedValue;
         if (propertyEnumeratedValue != null)
         {
            return propertyEnumeratedValue.EnumerationValues;
         }
         IfcPropertyListValue propertyListValue = simpleProperty as IfcPropertyListValue;
         if(propertyListValue != null)
         {
            return propertyListValue.NominalValue;
         }

         return null;
      }

      /// <summary>
      /// Returns the property value as a string, for SetValueString().
      /// </summary>
      /// <returns>The property value as a string.</returns>
      public static string PropertyValueAsString(this IfcSimpleProperty simpleProperty)
      {
         IfcPropertyBoundedValue propertyBoundedValue = simpleProperty as IfcPropertyBoundedValue;
         if (propertyBoundedValue != null)
            return propertyBoundedValue.PropertyValueAsString();

         IfcPropertyReferenceValue propertyReferenceValue = simpleProperty as IfcPropertyReferenceValue;
         if (propertyReferenceValue != null)
            return ""; //Not converting reference values;


         Importer.TheLog.LogUnhandledSubTypeError(simpleProperty, "IfcSimpleProperty", true);
         return "";
      }
   }
}