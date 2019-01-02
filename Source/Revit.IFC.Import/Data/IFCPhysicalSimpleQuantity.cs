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
using Autodesk.Revit.DB;
using Revit.IFC.Common.Utility;
using Revit.IFC.Common.Enums;
using Revit.IFC.Import.Enums;
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   /// <summary>
   /// Represents an IfcPhysicalSimpleQuantity.
   /// </summary>
   public static class IFCPhysicalSimpleQuantity
   {
      public static IFCUnit GetUnit(this IfcPhysicalSimpleQuantity physicalSimpleQuantity)
      {
         IfcUnit unit = physicalSimpleQuantity.Unit;
         if (unit == null)
            return IFCImportFile.TheFile.IFCUnits.GetIFCProjectUnit(physicalSimpleQuantity.GetUnitType());
         return new IFCUnit(unit);
      }
      
      public static UnitType GetUnitType(this IfcPhysicalSimpleQuantity physicalSimpleQuantity)
      {
         if(physicalSimpleQuantity is IfcQuantityLength)
            return UnitType.UT_Length;
         if (physicalSimpleQuantity is IfcQuantityArea)
            return UnitType.UT_Area;
         if(physicalSimpleQuantity is IfcQuantityCount || physicalSimpleQuantity is IfcQuantityTime)
            return UnitType.UT_Number;
         if (physicalSimpleQuantity is IfcQuantityVolume)
            return UnitType.UT_Volume;
         if (physicalSimpleQuantity is IfcQuantityWeight)
            return UnitType.UT_Mass;

         Importer.TheLog.LogWarning(physicalSimpleQuantity.StepId, "Can't determine unit type for IfcPhysicalSimpleQuantity of type: " + physicalSimpleQuantity.StepClassName, true);
         return UnitType.UT_Undefined;
      }

      /// <summary>
      /// Create a quantity for a given element.
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="element">The element being created.</param>
      /// <param name="parameterMap">The parameters of the element.  Cached for performance.</param>
      /// <param name="propertySetName">The name of the containing property set.</param>
      /// <param name="createdParameters">The names of the created parameters.</param>
      public static  void Create(this IfcPhysicalSimpleQuantity physicalSimpleQuantity, Document doc, Element element, IFCParameterSetByGroup parameterGroupMap, string propertySetName, ISet<string> createdParameters)
      {

         IFCUnit unit = physicalSimpleQuantity.GetUnit();
         double doubleValueToUse = unit != null ? unit.Convert(physicalSimpleQuantity.MeasureValue.Measure) : physicalSimpleQuantity.MeasureValue.Measure;

         Parameter existingParameter = null;
         string originalParameterName = physicalSimpleQuantity.Name + "(" + propertySetName + ")";
         string parameterName = originalParameterName;

         if (!parameterGroupMap.TryFindParameter(parameterName, out existingParameter))
         {
            int parameterNameCount = 2;
            while (createdParameters.Contains(parameterName))
            {
               parameterName = originalParameterName + " " + parameterNameCount;
               parameterNameCount++;
            }
            if (parameterNameCount > 2)
               Importer.TheLog.LogWarning(physicalSimpleQuantity.StepId, "Renamed parameter: " + originalParameterName + " to: " + parameterName, false);

            if (existingParameter == null)
            {
               UnitType unitType = physicalSimpleQuantity.GetUnitType();

               bool created = IFCPropertySet.AddParameterDouble(doc, element, parameterName, unitType, doubleValueToUse, physicalSimpleQuantity.StepId);
               if (created)
                  createdParameters.Add(parameterName);

               return;
            }
         }

         bool setValue = true;
         switch (existingParameter.StorageType)
         {
            case StorageType.String:
               existingParameter.Set(doubleValueToUse.ToString());
               break;
            case StorageType.Double:
               existingParameter.Set(doubleValueToUse);
               break;
            default:
               setValue = false;
               break;
         }

         if (!setValue)
            Importer.TheLog.LogError(physicalSimpleQuantity.StepId, "Couldn't create parameter: " + physicalSimpleQuantity.Name + " of storage type: " + existingParameter.StorageType.ToString(), false);
      }
   }
}