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
   /// Represents an IfcPropertyBoundedValue
   /// </summary>
   public static class IFCPropertyBoundedValue
   {
      private static string FormatBoundedValue(this IfcPropertyBoundedValue propertyBoundedValue, IfcValue propertyValue)
      {

         IfcUnit ifcUnit = propertyBoundedValue.Unit;
         if (ifcUnit != null)
         {
            IFCUnit unit = new IFCUnit(ifcUnit);
            return UnitFormatUtils.Format(IFCImportFile.TheFile.Document.GetUnits(), unit.UnitType, Convert.ToDouble(propertyValue.Value), true, false);
         }
         else
            return propertyValue.ValueString;
      }

      /// <summary>
      /// Returns the property value as a string, for SetValueString().
      /// </summary>
      /// <returns>The property value as a string.</returns>
      public static string PropertyValueAsString(this IfcPropertyBoundedValue propertyBoundedValue)
      {
         // Format as one of the following:
         // None: empty string
         // Lower only: >= LowValue
         // Upper only: <= UpperValue
         // Lower and Upper: [ LowValue - UpperValue ]
         // SetPointValue: (SetPointValue)
         // Lower, SetPointValue: >= LowValue (SetPointValue)
         // Upper, SetPointValue: >= UpperValue (SetPointValue)
         // Lower, Upper, SetPointValue: [ LowValue - UpperValue ] (SetPointValue)
         string propertyValueAsString = string.Empty;

         IfcValue lowerValue = propertyBoundedValue.LowerBoundValue;
         IfcValue upperValue = propertyBoundedValue.UpperBoundValue;
         IfcValue setValue = propertyBoundedValue.SetPointValue;
         
         if (lowerValue != null)
         {
            if (upperValue == null)
               propertyValueAsString += ">= ";
            else
               propertyValueAsString += "[ ";

            propertyValueAsString += FormatBoundedValue(propertyBoundedValue, lowerValue);
         }

         if (upperValue != null)
         {
            if (lowerValue == null)
               propertyValueAsString += "<= ";
            else
               propertyValueAsString += " - ";
            propertyValueAsString += FormatBoundedValue(propertyBoundedValue, upperValue);
            if (lowerValue != null)
               propertyValueAsString += " ]";
         }

         if (setValue != null)
         {
            if (upperValue != null || lowerValue != null)
               propertyValueAsString += " ";
            propertyValueAsString += "(" + FormatBoundedValue(propertyBoundedValue, setValue) + ")";
         }

         return propertyValueAsString;
      }
   }
}