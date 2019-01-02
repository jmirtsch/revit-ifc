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
using Revit.IFC.Common.Utility;
using Revit.IFC.Common.Enums;
using Revit.IFC.Import.Enums;
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   /// <summary>
   /// Represents an IfcObject.
   /// </summary>
   public static class IFCObject 
   {
      /// <summary>
      /// Gets the predefined type from the IfcObject, depending on the file version and entity type.
      /// </summary>
      /// <param name="ifcObjectDefinition">The associated handle.</param>
      /// <returns>The predefined type, if any.</returns>
      /// <remarks>Some entities use other fields as predefined type, including IfcDistributionPort ("FlowDirection") and IfcSpace (pre-IFC4).</remarks>
      internal static string GetPredefinedTypeOverride(this IfcObjectDefinition objectDefinition)
      {
         IfcObject ifcObject = objectDefinition as IfcObject;
         
         string predefinedType = ifcObject == null ? objectDefinition.GetPredefinedType() : ifcObject.GetPredefinedType(true);

         if (string.IsNullOrEmpty(predefinedType) || string.Compare(predefinedType, "NOTDEFINED", true) == 0)
         {
            IfcDistributionPort distributionPort = objectDefinition as IfcDistributionPort;
            if (distributionPort != null)
               return distributionPort.FlowDirection.ToString();
         // For IFC2x3, some entities have a "ShapeType" instead of a "PredefinedType", which we will check below.
            // The following have "PredefinedType", but are out of scope for now:
            // IfcCostSchedule, IfcOccupant, IfcProjectOrder, IfcProjectOrderRecord, IfcServiceLifeFactor
            // IfcStructuralAnalysisModel, IfcStructuralCurveMember, IfcStructuralLoadGroup, IfcStructuralSurfaceMember
            //if ((EntityType == IFCEntityType.IfcRamp) ||
            //    (EntityType == IFCEntityType.IfcRoof) ||
            //    (EntityType == IFCEntityType.IfcStair))
            //   predefinedTypeName = "ShapeType";

         }
         if (string.Compare(predefinedType, "NOTDEFINED", true) == 0)
            return null;
         return predefinedType;
      }
   }
}