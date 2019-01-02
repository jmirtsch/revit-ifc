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
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Revit.IFC.Common.Enums;
using Revit.IFC.Common.Utility;
using Revit.IFC.Import.Enums;
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   public static class IFCBuildingStorey
   {
      static ElementId m_ViewPlanTypeId = ElementId.InvalidElementId;

      static ElementId m_ExistingLevelIdToReuse = ElementId.InvalidElementId;

      /// <summary>
      /// Returns true if we have tried to set m_ViewPlanTypeId.  m_ViewPlanTypeId may or may not have a valid value.
      /// </summary>
      static bool m_ViewPlanTypeIdInitialized = false;

      /// <summary>
      /// If the ActiveView is level-based, we can't delete it.  Instead, use it for the first level "created".
      /// </summary>
      public static ElementId ExistingLevelIdToReuse
      {
         get { return m_ExistingLevelIdToReuse; }
         set { m_ExistingLevelIdToReuse = value; }
      }

      /// <summary>
      /// Get the default family type for creating ViewPlans.
      /// </summary>
      /// <param name="doc"></param>
      /// <returns></returns>
      public static ElementId GetViewPlanTypeId(Document doc)
      {
         if (m_ViewPlanTypeIdInitialized == false)
         {
            ViewFamily viewFamilyToUse = (doc.Application.Product == ProductType.Structure) ? ViewFamily.StructuralPlan : ViewFamily.FloorPlan;

            m_ViewPlanTypeIdInitialized = true;
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            ICollection<Element> viewFamilyTypes = collector.OfClass(typeof(ViewFamilyType)).ToElements();
            foreach (Element element in viewFamilyTypes)
            {
               ViewFamilyType viewFamilyType = element as ViewFamilyType;
               if (viewFamilyType.ViewFamily == viewFamilyToUse)
               {
                  m_ViewPlanTypeId = viewFamilyType.Id;
                  break;
               }
            }
         }
         return m_ViewPlanTypeId;
      }

      /// <summary>
      /// Creates or populates Revit elements based on the information contained in this class.
      /// </summary>
      /// <param name="doc">The document.</param>
      internal static void Create(this IfcBuildingStorey buildingStorey, CreateElementIfcCache cache)
      {
         Document doc = cache.Document;
         // We may re-use the ActiveView Level and View, since we can't delete them.
         // We will consider that we "created" this level and view for creation metrics.
         Level level = Importer.TheCache.UseElementByGUID<Level>(doc, buildingStorey.GlobalId);

         bool reusedLevel = false;
         bool foundLevel = false;

         if (level == null)
         {
            if (ExistingLevelIdToReuse != ElementId.InvalidElementId)
            {
               level = doc.GetElement(ExistingLevelIdToReuse) as Level;
               Importer.TheCache.UseElement(level);
               ExistingLevelIdToReuse = ElementId.InvalidElementId;
               reusedLevel = true;
            }
         }
         else
            foundLevel = true;

         if (level == null)
            level = Level.Create(doc, IFCUnitUtil.ScaleLength(buildingStorey.Elevation));
         else
            level.Elevation = IFCUnitUtil.ScaleLength(buildingStorey.Elevation);

         if (level != null)
            cache.CreatedElements[buildingStorey.StepId] = level.Id;

         if (level != null && level.Id != ElementId.InvalidElementId)
         {
            if (!foundLevel)
            {
               if (!reusedLevel)
               {
                  ViewPlan viewPlan = null;
                  ElementId viewPlanTypeId = IFCBuildingStorey.GetViewPlanTypeId(doc);
                  if (viewPlanTypeId != ElementId.InvalidElementId)
                  {
                     viewPlan = ViewPlan.Create(doc, viewPlanTypeId, level.Id);
                     if (viewPlan != null)
                        cache.CreatedViews[buildingStorey.StepId] = viewPlan.Id;
                  }

                  if (viewPlan == null)
                     Importer.TheLog.LogAssociatedCreationError(buildingStorey, typeof(ViewPlan));
               }
               else
               {
                  if (doc.ActiveView != null)
                     cache.CreatedViews[buildingStorey.StepId] = doc.ActiveView.Id;
               }
            }
         }
         else
            Importer.TheLog.LogCreationError(buildingStorey, null, false);

      }
   }
}