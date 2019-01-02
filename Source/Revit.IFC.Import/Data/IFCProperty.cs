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
using Autodesk.Revit.DB.IFC;
using Autodesk.Revit.DB;
using Revit.IFC.Common.Utility;
using Revit.IFC.Common.Enums;
using Revit.IFC.Import.Properties;
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   /// <summary>
   /// Represents an IfcProperty.
   /// </summary>
   public static class IFCProperty
   {

      public static string PropertyValueAsString(this IfcProperty property)
      {
         IfcSimpleProperty simpleProperty = property as IfcSimpleProperty;
         if (simpleProperty != null)
            return simpleProperty.PropertyValueAsString();
         IfcComplexProperty complexProperty = property as IfcComplexProperty;
         if (complexProperty != null)
            return complexProperty.PropertyValueAsString();

         Importer.TheLog.LogUnhandledSubTypeError(property, "IfcProperty", true);
         return "";

      }
      private static bool IsValidParameterType(this IfcProperty property, Parameter parameter, IFCDataPrimitiveType dataType)
      {
         switch (parameter.StorageType)
         {
            case StorageType.String:
               if (dataType == IFCDataPrimitiveType.String ||
                   dataType == IFCDataPrimitiveType.Enumeration ||
                   dataType == IFCDataPrimitiveType.Binary ||
                   dataType == IFCDataPrimitiveType.Double ||
                   dataType == IFCDataPrimitiveType.Integer ||
                   dataType == IFCDataPrimitiveType.Boolean ||
                   dataType == IFCDataPrimitiveType.Logical)
                  return true;
               break;
            case StorageType.Integer:
               if (dataType == IFCDataPrimitiveType.Integer ||
                   dataType == IFCDataPrimitiveType.Boolean ||
                   dataType == IFCDataPrimitiveType.Logical)
                  return true;
               break;
            case StorageType.Double:
               if (dataType == IFCDataPrimitiveType.Double ||
                   dataType == IFCDataPrimitiveType.Integer ||
                   dataType == IFCDataPrimitiveType.Boolean ||
                   dataType == IFCDataPrimitiveType.Logical)
                  return true;
               break;
         }

         return false;
      }

      /// <summary>
      /// Create a property for a given element.
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="element">The element being created.</param>
      /// <param name="parameterMap">The parameters of the element.  Cached for performance.</param>
      /// <param name="propertySetName">The name of the containing property set.</param>
      /// <param name="createdParameters">The names of the created parameters.</param>
      public static void Create(this IfcProperty property, CreateElementIfcCache cache, Element element, IFCParameterSetByGroup parameterGroupMap, string propertySetName, ISet<string> createdParameters)
      {
         Document doc = cache.Document;
         // Try to get the single value from the property.  If we can't get a single value, get it as a string.
         IfcValue propertyValueToUse = null;
         IFCUnit unit = null;
         BaseClassIfc objectReferenceSelect = null;
         IfcSimpleProperty simpleProperty = property as IfcSimpleProperty; 
         if (simpleProperty != null)
         {
            IList<IfcValue> propertyValues = simpleProperty.GetPropertyValues();
            if (propertyValues != null && propertyValues.Count == 1)
            {
               // If the value isn't set, skip it.  We won't warn.
               if (propertyValues[0] == null)
                  return;

               propertyValueToUse = propertyValues[0];
            }
            unit = simpleProperty.GetUnit(); 
         }
         else
         {
            IfcPropertyReferenceValue propertyReferenceValue = property as IfcPropertyReferenceValue;
            if(propertyReferenceValue != null)
            {
               objectReferenceSelect = propertyReferenceValue.PropertyReference as BaseClassIfc;
            }
         }

         IFCDataPrimitiveType dataType = IFCDataPrimitiveType.Unknown;
         UnitType unitType = UnitType.UT_Undefined;

         bool? boolValueToUse = null;
         IfcLogicalEnum? logicalValueToUse = null;
         int? intValueToUse = null;
         double? doubleValueToUse = null;
         ElementId elementIdValueToUse = null;
         string stringValueToUse = null;

         if(objectReferenceSelect != null)
         {
            ElementId propertyValueAsId;
            cache.CreatedElements.TryGetValue(objectReferenceSelect.StepId, out propertyValueAsId);
            if (propertyValueAsId != ElementId.InvalidElementId)
            {
               elementIdValueToUse = propertyValueAsId;
               dataType = IFCDataPrimitiveType.Instance;
            }
            else
            {
               NamedObjectIfc namedObject = objectReferenceSelect as NamedObjectIfc;
               if(namedObject != null)
                  stringValueToUse = namedObject.Name;
               else
                  stringValueToUse =  objectReferenceSelect.ToString();
               dataType = IFCDataPrimitiveType.String;
            }
         }
         else if (propertyValueToUse == null)
         {
            string propertyValueAsString = property.PropertyValueAsString();
            if (propertyValueAsString == null)
            {
               Importer.TheLog.LogError(property.StepId, "Couldn't create parameter: " + property.Name, false);
               return;
            }

            dataType = IFCDataPrimitiveType.String;
            stringValueToUse = propertyValueAsString;
         }
         else
         {
            if (propertyValueToUse.ValueType == typeof(string) || propertyValueToUse is IfcBinary)
            {
               stringValueToUse = propertyValueToUse.ValueString;
               dataType = IFCDataPrimitiveType.String;
            }
            else
            {
               IfcInteger ifcInteger = propertyValueToUse as IfcInteger;
               if (ifcInteger != null)
               {
                  intValueToUse = ifcInteger.Magnitude;
                  dataType = IFCDataPrimitiveType.Integer;
               }
               else
               {
                  IfcBoolean boolean = propertyValueToUse as IfcBoolean;
                  if (boolean != null)
                  {
                     boolValueToUse = boolean.Boolean;
                     dataType = IFCDataPrimitiveType.Boolean;
                  }
                  else
                  {
                     IfcLogical logical = propertyValueToUse as IfcLogical;
                     if (logical != null)
                     {
                        logicalValueToUse = logical.Logical;
                        dataType = IFCDataPrimitiveType.Logical;
                     }
                     else if (propertyValueToUse.ValueType == typeof(double))
                     {
                        dataType = IFCDataPrimitiveType.Double;
                        if (unit == null)
                        {
                           UnitType propertyUnitType = IFCDataUtil.GetUnitTypeFromData(propertyValueToUse, UnitType.UT_Undefined);
                           if (propertyUnitType != UnitType.UT_Undefined)
                              unit = IFCImportFile.TheFile.IFCUnits.GetIFCProjectUnit(unitType);
                        }
                        if (unit == null)
                           doubleValueToUse = Convert.ToDouble(propertyValueToUse.Value);
                        else
                           doubleValueToUse = Convert.ToDouble(propertyValueToUse) * unit.ScaleFactor;
                     }
                     else
                     {
                        Importer.TheLog.LogError(property.StepId, "Unknown value type for parameter: " + property.Name, false);
                        stringValueToUse = propertyValueToUse.ValueString;
                     }
                  }
               }
            }
         }

         Parameter existingParameter = null;
         bool elementIsType = (element is ElementType);
         string typeString = elementIsType ? " " + Resources.IFCTypeSchedule : string.Empty;
         string originalParameterName = property.Name + "(" + propertySetName + typeString + ")";
         string parameterName = originalParameterName;

         if (parameterGroupMap.TryFindParameter(parameterName, out existingParameter))
         {
            if ((existingParameter != null) && !property.IsValidParameterType(existingParameter, dataType))
               existingParameter = null;
         }

         if (existingParameter == null)
         {
            int parameterNameCount = 2;
            while (createdParameters.Contains(parameterName))
            {
               parameterName = originalParameterName + " " + parameterNameCount;
               parameterNameCount++;
            }
            if (parameterNameCount > 2)
               Importer.TheLog.LogWarning(property.StepId, "Renamed parameter: " + originalParameterName + " to: " + parameterName, false);

            bool created = false;
            switch (dataType)
            {
               case IFCDataPrimitiveType.String:
               case IFCDataPrimitiveType.Enumeration:
               case IFCDataPrimitiveType.Binary:
                  created = IFCPropertySet.AddParameterString(doc, element, parameterName, stringValueToUse, property.StepId);
                  break;
               case IFCDataPrimitiveType.Integer:
                  created = IFCPropertySet.AddParameterInt(doc, element, parameterName, intValueToUse.Value, property.StepId);
                  break;
               case IFCDataPrimitiveType.Boolean:
                  created = IFCPropertySet.AddParameterBoolean(doc, element, parameterName, boolValueToUse.Value, property.StepId);
                  break;
               case IFCDataPrimitiveType.Logical:
                  if (logicalValueToUse != IfcLogicalEnum.UNKNOWN)
                     created = IFCPropertySet.AddParameterBoolean(doc, element, parameterName, (logicalValueToUse == IfcLogicalEnum.TRUE), property.StepId);
                  break;
               case IFCDataPrimitiveType.Double:
                  created = IFCPropertySet.AddParameterDouble(doc, element, parameterName, unitType, doubleValueToUse.Value, property.StepId);
                  break;
               case IFCDataPrimitiveType.Instance:
                  created = IFCPropertySet.AddParameterElementId(doc, element, parameterName, elementIdValueToUse, property.StepId);
                  break;
            }

            if (created)
               createdParameters.Add(originalParameterName);

            return;
         }

         bool couldSetValue = false;
         switch (existingParameter.StorageType)
         {
            case StorageType.String:
               {
                  switch (dataType)
                  {
                     case IFCDataPrimitiveType.String:
                     case IFCDataPrimitiveType.Enumeration:
                     case IFCDataPrimitiveType.Binary:
                        couldSetValue = existingParameter.Set(stringValueToUse);
                        break;
                     case IFCDataPrimitiveType.Integer:
                        couldSetValue = existingParameter.Set(intValueToUse.Value.ToString());
                        break;
                     case IFCDataPrimitiveType.Boolean:
                        couldSetValue = existingParameter.Set(boolValueToUse.Value ? "True" : "False");
                        break;
                     case IFCDataPrimitiveType.Logical:
                        couldSetValue = existingParameter.Set(logicalValueToUse.ToString());
                        break;
                     case IFCDataPrimitiveType.Double:
                        couldSetValue = existingParameter.Set(doubleValueToUse.ToString());
                        break;
                     default:
                        break;
                  }
               }
               break;
            case StorageType.Integer:
               if (dataType == IFCDataPrimitiveType.Integer)
                  couldSetValue = existingParameter.Set(intValueToUse.Value);
               else if (dataType == IFCDataPrimitiveType.Boolean)
                  couldSetValue = existingParameter.Set(boolValueToUse.Value ? 1 : 0);
               else if (dataType == IFCDataPrimitiveType.Logical)
                  couldSetValue = (logicalValueToUse == IfcLogicalEnum.UNKNOWN) ? true : existingParameter.Set((logicalValueToUse == IfcLogicalEnum.TRUE) ? 1 : 0);
               break;
            case StorageType.Double:
               if (dataType == IFCDataPrimitiveType.Double)
                  couldSetValue = existingParameter.Set(doubleValueToUse.Value);
               else if (dataType == IFCDataPrimitiveType.Integer)
                  couldSetValue = existingParameter.Set(intValueToUse.Value);
               else if (dataType == IFCDataPrimitiveType.Boolean)
                  couldSetValue = existingParameter.Set(boolValueToUse.Value ? 1 : 0);
               else if ((dataType == IFCDataPrimitiveType.Logical) && (logicalValueToUse != IfcLogicalEnum.UNKNOWN))
                  couldSetValue = existingParameter.Set((logicalValueToUse == IfcLogicalEnum.TRUE) ? 1 : 0);
               break;
         }

         if (!couldSetValue)
            Importer.TheLog.LogError(property.StepId, "Couldn't create parameter: " + property.Name + " of storage type: " + existingParameter.StorageType.ToString(), false);
      }
   }
}