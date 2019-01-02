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
using Revit.IFC.Common.Enums;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Revit.IFC.Common.Utility;
using Revit.IFC.Import.Enums;
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

using TemporaryDisableLogging = Revit.IFC.Import.Utility.IFCImportOptions.TemporaryDisableLogging;

namespace Revit.IFC.Import.Data
{
   /// <summary>
   /// Represents an IfcElement.
   /// </summary>
   /// <remarks>This class is non-abstract until all derived classes are defined.</remarks>
   public static class IFCElement 
   {
      /// <summary>
      /// Creates or populates Revit element params based on the information contained in this class.
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="element">The element.</param>
      internal static void CreateParametersInternal(this IfcElement ifcElement, Document doc, Element element)
      {
         if (element != null)
         {
            // Set "Tag" parameter.
            string ifcTag = ifcElement.Tag;
            if (!string.IsNullOrWhiteSpace(ifcTag))
               IFCPropertySet.AddParameterString(doc, element, "IfcTag", ifcTag, ifcElement.StepId);


            IfcRelFillsElement relFillsElement = ifcElement.FillsVoids.FirstOrDefault();
            if (relFillsElement != null)
            {
               IfcFeatureElementSubtraction ifcFeatureElementSubtraction = relFillsElement.RelatingOpeningElement;
               if (ifcFeatureElementSubtraction != null)
               {
                  IfcRelVoidsElement relVoidsElement = ifcFeatureElementSubtraction.VoidsElement;
                  if (relVoidsElement != null)
                  {
                     IfcElement host = relVoidsElement.RelatingBuildingElement;
                     if (ifcElement != null)
                     {
                        string ifcContainerName = ifcElement.Name;
                        IFCPropertySet.AddParameterString(doc, element, "IfcContainedInHost", ifcContainerName, ifcElement.StepId);
                     }
                  }
               }
            }

            // Create two parameters for each port: one for name, and one for GUID.
            int numPorts = 0;
            List<IfcPort> ports = ifcElement.HasPortsSS.Select(x=>x.RelatingPort).ToList();
            ports.AddRange(ifcElement.IsDecomposedBy.SelectMany(x => x.RelatedObjects).OfType<IfcPort>());
            foreach (IfcPort port in ports)
            {
               string name = port.Name;
               string guid = port.GlobalId;

               if (!string.IsNullOrWhiteSpace(name))
               {
                  string parameterName = "IfcElement HasPorts Name " + ((numPorts == 0) ? "" : (numPorts + 1).ToString());
                  IFCPropertySet.AddParameterString(doc, element, parameterName, name, ifcElement.StepId);
               }

               if (!string.IsNullOrWhiteSpace(guid))
               {
                  string parameterName = "IfcElement HasPorts IfcGUID " + ((numPorts == 0) ? "" : (numPorts + 1).ToString());
                  IFCPropertySet.AddParameterString(doc, element, parameterName, guid, ifcElement.StepId);
               }

               numPorts++;
            }

            IfcElementAssembly elementAssembly = ifcElement as IfcElementAssembly;
            if(elementAssembly != null)
            {
               IFCPropertySet.AddParameterString(doc, element, "IfcPredefinedType", elementAssembly.PredefinedType.ToString(), elementAssembly.StepId);
               IFCPropertySet.AddParameterString(doc, element, "IfcAssemblyPlace", elementAssembly.AssemblyPlace.ToString(), elementAssembly.StepId);
            }
         }
      }
   }
}