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
   /// Represents an IfcSpace.
   /// </summary>
   public static class IFCSpace
   {
      // IFC2x3 has "InteriorOrExteriorSpace"; IFC4 has "PredefinedType".  We will use the IFCObjectDefinition PredefinedType to store this field.

      /// <summary>
      /// Creates or populates Revit element params based on the information contained in this class.
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="element">The element.</param>
      internal static void CreateParametersInternal(this IfcSpace space, Document doc, Element element)
      {
         if (element != null)
         {
            // Set "ElevationWithFlooring" parameter.
            IFCPropertySet.AddParameterDouble(doc, element, "ElevationWithFlooring", UnitType.UT_Length, space.ElevationWithFlooring, space.StepId);

            if(space.Database.Release < ReleaseVersion.IFC4)
               IFCPropertySet.AddParameterString(doc, element, "InteriorOrExteriorSpace", space.PredefinedType.ToString(), space.StepId);

            // Set "IfcZone" parameter.
            string zoneNames = null;
            foreach (IfcZone zone in space.HasAssignments.OfType<IfcRelAssignsToGroup>().Select(x=>x.RelatingGroup).OfType<IfcZone>())
            {
               string name = zone.Name;
               if (string.IsNullOrWhiteSpace(name))
                  continue;

               if (zoneNames == null)
                  zoneNames = name;
               else
                  zoneNames += ";" + name;
            }

            if (zoneNames != null)
               IFCPropertySet.AddParameterString(doc, element, "IfcZone", zoneNames, space.StepId);
         }
      }
   }
}