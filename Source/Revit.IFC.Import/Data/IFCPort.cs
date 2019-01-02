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
using Revit.IFC.Import.Geometry;
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   /// <summary>
   /// Represents an IfcPort.
   /// </summary>
   public static class IFCPort
   {
      /// <summary>
      /// Creates or populates Revit element params based on the information contained in this class.
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="element">The element.</param>
      internal static void CreateParametersInternal(this IfcPort port, Document doc, Element element)
      {
         if (element != null)
         {
            IfcObjectDefinition containedIn = null;
            IfcRelConnectsPortToElement relConnectsPortToElement = port.ContainedIn;
            if (relConnectsPortToElement != null)
               containedIn = relConnectsPortToElement.RelatedElement;
            else
            {
               IfcRelNests relNests = port.Nests;
               if (relNests != null)
                  containedIn = relNests.RelatingObject;
            }
            if(containedIn != null)
            {
               string guid = containedIn.GlobalId;
               if (!string.IsNullOrWhiteSpace(guid))
                  IFCPropertySet.AddParameterString(doc, element, "IfcElement ContainedIn IfcGUID", guid, port.StepId);

               string name = containedIn.Name;
               if (!string.IsNullOrWhiteSpace(name))
                  IFCPropertySet.AddParameterString(doc, element, "IfcElement ContainedIn Name", name, port.StepId);
            }
            IfcRelConnectsPorts relConnectsPorts = port.ConnectedFrom;
            if (relConnectsPorts != null)
            {
               IfcPort connectedFrom = relConnectsPorts.ConnectedPort(port);
               string guid = connectedFrom.GlobalId;
               if (!string.IsNullOrWhiteSpace(guid))
                  IFCPropertySet.AddParameterString(doc, element, "IfcPort ConnectedFrom IfcGUID", guid, port.StepId);

               string name = connectedFrom.Name;
               if (!string.IsNullOrWhiteSpace(name))
                  IFCPropertySet.AddParameterString(doc, element, "IfcPort ConnectedFrom Name", name, port.StepId);
            }
            relConnectsPorts = port.ConnectedTo;
            if(relConnectsPorts != null)
            {
               IfcPort connectedTo = relConnectsPorts.ConnectedPort(port);
               string guid = connectedTo.GlobalId;
               if (!string.IsNullOrWhiteSpace(guid))
                  IFCPropertySet.AddParameterString(doc, element, "IfcPort ConnectedTo IfcGUID", guid, port.StepId);

               string name = connectedTo.Name;
               if (!string.IsNullOrWhiteSpace(name))
                  IFCPropertySet.AddParameterString(doc, element, "IfcPort ConnectedTo Name", name, port.StepId);
            }
         }
      }
   }
}