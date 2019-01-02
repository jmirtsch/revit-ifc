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
using Revit.IFC.Common.Utility;
using Revit.IFC.Common.Enums;
using Revit.IFC.Import.Properties;
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   /// <summary>
   /// Represents an IfcPropertySetDefinition.
   /// </summary>
   public static class IFCPropertySetDefinition 
   {
      static IDictionary<IFCEntityType, int> m_DoorWindowPanelCounters = new Dictionary<IFCEntityType, int>();

      /// <summary>
      /// Reset the counters that will keep track of the number of IfcDoorPanelProperties and IfcWindowPanelProperties this IfcObject has.
      /// </summary>
      public static void ResetCounters()
      {
         m_DoorWindowPanelCounters.Clear();
      }

      public static int GetNextCounter(IFCEntityType type)
      {
         int nextValue;
         if (!m_DoorWindowPanelCounters.TryGetValue(type, out nextValue))
            nextValue = 0;
         m_DoorWindowPanelCounters[type] = ++nextValue;
         return nextValue;
      }

      /// <summary>
      /// Create a schedule for a given property set.
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="element">The element being created.</param>
      /// <param name="parameterGroupMap">The parameters of the element.  Cached for performance.</param>
      /// <param name="parametersCreated">The created parameters.</param>
      internal static void CreateScheduleForPropertySet(this IfcPropertySetDefinition propertySetDefinition, Document doc, Element element, IFCParameterSetByGroup parameterGroupMap, ISet<string> parametersCreated)
      {
         if (parametersCreated.Count == 0)
            return;

         Category category = element.Category;
         if (category == null)
            return;

         ElementId categoryId = category.Id;
         bool elementIsType = (element is ElementType);

         Tuple<ElementId, bool, string> scheduleKey = new Tuple<ElementId, bool, string>(categoryId, elementIsType, propertySetDefinition.Name);

         ISet<string> viewScheduleNames = Importer.TheCache.ViewScheduleNames;
         IDictionary<Tuple<ElementId, bool, string>, ElementId> viewSchedules = Importer.TheCache.ViewSchedules;

         ElementId viewScheduleId;
         if (!viewSchedules.TryGetValue(scheduleKey, out viewScheduleId))
         {
            string scheduleName = scheduleKey.Item3;
            string scheduleTypeName = elementIsType ? " " + Resources.IFCTypeSchedule : string.Empty;

            int index = 1;
            while (viewScheduleNames.Contains(scheduleName))
            {
               string indexString = (index > 1) ? " " + index.ToString() : string.Empty;
               scheduleName += " (" + category.Name + scheduleTypeName + indexString + ")";
               index++;
               if (index > 1000)
               {
                  Importer.TheLog.LogWarning(propertySetDefinition.StepId, "Too many property sets with the name " + scheduleKey.Item3 +
                     ", no longer creating schedules with that name.", true);
                  return;
               }
            }

            // Not all categories allow creating schedules.  Skip these.
            ViewSchedule viewSchedule = null;
            try
            {
               viewSchedule = ViewSchedule.CreateSchedule(doc, scheduleKey.Item1);
            }
            catch
            {
               // Only try to create the schedule once per key.
               viewSchedules[scheduleKey] = ElementId.InvalidElementId;
               return;
            }

            if (viewSchedule != null)
            {    
               viewSchedule.Name = scheduleName;
               viewSchedules[scheduleKey] = viewSchedule.Id;
               viewScheduleNames.Add(scheduleName);

               ElementId ifcGUIDId = new ElementId(elementIsType ? BuiltInParameter.IFC_TYPE_GUID : BuiltInParameter.IFC_GUID);
               string propertySetListName = elementIsType ? Resources.IFCTypeSchedule + " IfcPropertySetList" : "IfcPropertySetList";

               IList<SchedulableField> schedulableFields = viewSchedule.Definition.GetSchedulableFields();

               bool filtered = false;
               foreach (SchedulableField sf in schedulableFields)
               {
                  string fieldName = sf.GetName(doc);
                  if (parametersCreated.Contains(fieldName) || sf.ParameterId == ifcGUIDId)
                  {
                     viewSchedule.Definition.AddField(sf);
                  }
                  else if (!filtered && fieldName == propertySetListName)
                  {
                     // We want to filter the schedule for specifically those elements that have this property set assigned.
                     ScheduleField scheduleField = viewSchedule.Definition.AddField(sf);
                     scheduleField.IsHidden = true;
                     ScheduleFilter filter = new ScheduleFilter(scheduleField.FieldId, ScheduleFilterType.Contains, "\"" + propertySetDefinition.Name + "\"");
                     viewSchedule.Definition.AddFilter(filter);
                     filtered = true;
                  }
               }
            }
         }

         return;
      }

      /// <summary>
      /// Create a property set for a given element.
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="element">The element being created.</param>
      /// <param name="parameterGroupMap">The parameters of the element.  Cached for performance.</param>
      /// <returns>The name of the property set created, if it was created, and a Boolean value if it should be added to the property set list.</returns>
      public static KeyValuePair<string, bool> CreatePropertySet(this IfcPropertySetDefinition propertySetDefinition, CreateElementIfcCache cache, Element element, IFCParameterSetByGroup parameterGroupMap)
      {
         Document doc = cache.Document;
         string quotedName = "\"" + propertySetDefinition.Name + "\"";

         ISet<string> parametersCreated = new HashSet<string>();

         IfcElementQuantity elementQuantity = propertySetDefinition as IfcElementQuantity;
         if(elementQuantity != null)
         {
            foreach (IfcPhysicalSimpleQuantity quantity in elementQuantity.Quantities.Values.OfType<IfcPhysicalSimpleQuantity>())
            {
               quantity.Create(doc, element, parameterGroupMap, elementQuantity.Name, parametersCreated);
            }

            elementQuantity.CreateScheduleForPropertySet(doc, element, parameterGroupMap, parametersCreated);
            return new KeyValuePair<string, bool>(quotedName, true);
         }
         IfcPropertySet propertySet = propertySetDefinition as IfcPropertySet;
         if(propertySet != null)
         {
            foreach (IfcProperty property in propertySet.HasProperties.Values)
            {
               property.Create(cache, element, parameterGroupMap, propertySet.Name, parametersCreated);
            }

            propertySet.CreateScheduleForPropertySet(doc, element, parameterGroupMap, parametersCreated);
            return new KeyValuePair<string, bool>(quotedName, true);
         }

         return new KeyValuePair<string, bool>(null, false);
      }
   }
}