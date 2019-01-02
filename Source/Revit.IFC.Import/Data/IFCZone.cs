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
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Revit.IFC.Common.Utility;
using Revit.IFC.Common.Enums;
using Revit.IFC.Import;
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   /// <summary>
   /// Represents an IfcZone.
   /// </summary>
   public static class IFCZone
   {
      /// <summary>
      /// Creates or populates Revit elements based on the information contained in this class.
      /// </summary>
      /// <param name="doc">The document.</param>
      internal static ElementId CreateZone(this IfcZone zone, CreateElementIfcCache cache, Transform globalTransform)
      {
         ElementId elementId;
         if (cache.CreatedElements.TryGetValue(zone.StepId, out elementId))
            return elementId;
         // If we created an element above, then we will set the shape of it to be the same of the shapes of the contained spaces.
         List<GeometryObject> geomObjs = new List<GeometryObject>();

         // CreateDuplicateZoneGeometry is currently an API-only option (no UI), set to true by default.
         if (Importer.TheOptions.CreateDuplicateZoneGeometry)
         {
            foreach (IfcSpace space in zone.IsGroupedBy.SelectMany(x=>x.RelatedObjects).OfType<IfcSpace>())
            {
               Tuple<IList<IFCSolidInfo>, IList<Curve>> spaceGeometry = space.ConvertRepresentation(cache, globalTransform, false, zone);
               // This lets us create a copy of the space geometry with the Zone graphics style.
               geomObjs.AddRange(spaceGeometry.Item1.Select(x=>x.GeometryObject));
            }
         }

         DirectShape zoneElement = IFCElementUtil.CreateElement(cache, zone.CategoryId(cache), zone.GlobalId, geomObjs, zone.StepId);
         if (zoneElement != null)
            return zoneElement.Id;
         return ElementId.InvalidElementId;
      }
   }
}