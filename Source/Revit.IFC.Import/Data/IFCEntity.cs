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
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Revit.IFC.Common.Utility;
using Revit.IFC.Common.Enums;
using Revit.IFC.Import.Enums;
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   /// <summary>
   /// Base level class for all objects created from an IFC entity.
   /// </summary>
   /// 
   public static class IFCEntity
   {

      public static IFCEntityType GetEntityType(this BaseClassIfc baseClass)
      {
         IFCEntityType entityType;
         if (Enum.TryParse<IFCEntityType>(baseClass.StepClassName, out entityType))
            return entityType;
         return IFCEntityType.UnKnown;
      }
      /// <summary>
      /// Check if two IFCEntity lists are equal.
      /// </summary>
      /// <param name="list1">The first list.</param>
      /// <param name="list2">The second list.</param>
      /// <returns>True if they are equal, false otherwise.</returns>
      /// <remarks>The is not intended to be an exhaustive check.</remarks>
      static public bool AreIFCEntityListsEquivalent<T>(IList<T> list1, IList<T> list2) where T : IBaseClassIfc
      {
         int numItems = list1.Count;
         if (numItems != list2.Count)
            return false;

         for (int ii = 0; ii < numItems; ii++)
         {
            if (!IFCRoot.Equals(list1[ii], list2[ii]))
               return false;
         }

         return true;
      }

      /// <summary>
      /// Does a top-level check to see if this entity may be equivalent to otherEntity.
      /// </summary>
      /// <param name="otherEntity">The other IFCEntity.</param>
      /// <returns>True if they are equivalent, false if they aren't, null if not enough information.</returns>
      /// <remarks>This isn't intended to be an exhaustive check, and isn't implemented for all types.  This is intended
      /// to be used by derived classes.</remarks>
      public static bool? MaybeEquivalentTo(this BaseClassIfc entity, BaseClassIfc otherEntity)
      {
         if (otherEntity == null)
            return false;

         // If the entities have the same Id, they are definitely the same object.  If they don't, they could
         // still be considered equivalent, so we won't disqualify them.
         if (entity.StepId == otherEntity.StepId)
            return true;

         if (string.Compare(entity.StepClassName, otherEntity.StepClassName, false) != 0)
            return false;

         IfcPresentationLayerAssignment presentationLayerAssignment = entity as IfcPresentationLayerAssignment;
         if (presentationLayerAssignment != null)
            return presentationLayerAssignment.MaybeEquivalentTo(otherEntity as IfcPresentationLayerAssignment);

         IfcStyledItem styledItem = entity as IfcStyledItem;
         if (styledItem != null)
            return styledItem.MaybeEquivalentTo(otherEntity as IfcStyledItem);

         return null;
      }

      /// <summary>
      /// Does a top-level check to see if this entity is equivalent to otherEntity.
      /// </summary>
      /// <param name="otherEntity">The other IFCEntity.</param>
      /// <returns>True if they are equivalent, false if they aren't.</returns>
      /// <remarks>This isn't intended to be an exhaustive check, and isn't implemented for all types.  This is intended
      /// to make a final decision, and will err on the side of deciding that entities aren't equivalent.</remarks>
      public static bool IsEquivalentTo(BaseClassIfc entity, BaseClassIfc otherEntity)
      {
         bool? maybeEquivalentTo = entity.MaybeEquivalentTo(otherEntity);
         if (maybeEquivalentTo.HasValue)
            return maybeEquivalentTo.Value;

         
         // If we couldn't determine that they were the same, assume that they aren't.
         return false;
      }
   }

   public class CreateElementIfcCache
   {
      internal UV TrueNorth = null;
      internal HashSet<int> InvalidForCreation = new HashSet<int>();
      internal Dictionary<int, ElementId> CreatedElements = new Dictionary<int, ElementId>();
      internal Dictionary<int, ElementId> CreatedViews = new Dictionary<int, ElementId>();
      internal Dictionary<int, IFCSimpleProfile> Profiles = new Dictionary<int, IFCSimpleProfile>();
      internal Dictionary<int, ElementId> PresentationLayerMaterials = new Dictionary<int, ElementId>();
      
      //Cache for selected entity geometry including IfcSpace
      internal Dictionary<int, IList<GeometryObject>> CachedGeometry = new Dictionary<int, IList<GeometryObject>>();

      internal Document Document { get; set; } = null;

      internal CreateElementIfcCache(Document document)
      {
         Document = document;
      }
   }
   
}