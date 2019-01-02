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
using Revit.IFC.Import.Geometry;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   public class ProjectAggregate
   {
      internal Transform WorldCoordinateSystem { get; set; } = Transform.Identity;

      internal Dictionary<int, IfcProduct> Elements { get; } = new Dictionary<int, IfcProduct>();
      internal List<IfcGrid> Grids = new List<IfcGrid>();
      internal Dictionary<string, IfcGridAxis> GridAxes { get; } = new Dictionary<string, IfcGridAxis>();
      internal List<IfcBuildingStorey> Stories { get; } = new List<IfcBuildingStorey>();
      internal List<IfcSpace> Spaces { get; } = new List<IfcSpace>();
      internal List<IfcSystem> Systems { get; } = new List<IfcSystem>();
      internal List<IfcZone> Zones { get; } = new List<IfcZone>();

      internal IFCUnits UnitsInContext { get; } = new IFCUnits();
      public IfcProject Project { get; private set; } = null;

      private CreateElementIfcCache m_Cache = null;

      
      // This is true if all of the contained entities inside of IfcSite have the local placement relative to the IfcSite's.
      // If this is set to true, we can move the project closer to the origin and set the project location; otherwise we can't do that easily.

      public ProjectAggregate(IfcProject project, Document document)
      {
         Project = project;
         m_Cache = new CreateElementIfcCache(document);
         Initialize();

         IfcObjectDefinition rootElement = project.RootElement();
         IfcBuilding building = project.UppermostBuilding();
         if(building != null)
         {
            if (building.Decomposes != null) // root Element should be site hosting building
               rootElement = building.Decomposes.RelatingObject;
         }

         bool canRemoveSitePlacement = rootElement.AddToAggregate(this, m_Cache);
         IfcSite site = rootElement as IfcSite;
         if (site != null)
            site.SetHostSite(m_Cache, document, canRemoveSitePlacement);
         
      }

      private void Initialize()
      {
         if (Project == null)
            return;
         IfcUnitAssignment unitAssignment = Project.UnitsInContext;
         if(unitAssignment != null)
         {
            foreach(IfcUnit unit in unitAssignment.Units)
            { 
               IFCUnit ifcUnit = IFCImportFile.TheFile.IFCUnits.ProcessIFCProjectUnit(unit);
            }
         }

         // process true north - take the first valid representation context that has a true north value.
         foreach(IfcGeometricRepresentationContext context in Project.RepresentationContexts.OfType<IfcGeometricRepresentationContext>())
         {
            if (m_Cache.TrueNorth == null && context.TrueNorth != null)
            {
               // TODO: Verify that we don't have inconsistent true norths.  If we do, warn.
               XYZ xyz = context.TrueNorth.ProcessNormalizedIFCDirection();
               m_Cache.TrueNorth = new UV(xyz.X, xyz.Y);
            }

            if (WorldCoordinateSystem == null && context.WorldCoordinateSystem != null)
            {
                WorldCoordinateSystem = context.WorldCoordinateSystem.GetAxis2PlacementTransform();
            }
         }
      }
      /// <summary>
      /// Creates or populates Revit elements based on the information contained in this class.
      /// </summary>
      /// <param name="doc">The document.</param>
      public void AddElements()
      {
         Document doc = m_Cache.Document;
         Units documentUnits = new Units(doc.DisplayUnitSystem == DisplayUnit.METRIC ?
             UnitSystem.Metric : UnitSystem.Imperial);
         foreach (IFCUnit unit in UnitsInContext.m_ProjectUnitsDictionary.Values)
         {
            if (unit != null)
            {
               try
               {
                  FormatOptions formatOptions = new FormatOptions(unit.UnitName);
                  formatOptions.UnitSymbol = unit.UnitSymbol;
                  documentUnits.SetFormatOptions(unit.UnitType, formatOptions);
               }
               catch (Exception ex)
               {
                  Importer.TheLog.LogError(unit.Id, ex.Message, false);
               }
            }
         }
         doc.SetUnits(documentUnits);
         // Use the ProjectInfo element in the document to store its parameters.
         Project.CreateParameters(m_Cache, Importer.TheCache.ProjectInformationId, new HashSet<string>());

         // We will randomize unused grid names so that they don't conflict with new entries with the same name.
         // This is only for relink.
         foreach (ElementId gridId in Importer.TheCache.GridNameToElementMap.Values)
         {
            Grid grid = doc.GetElement(gridId) as Grid;
            if (grid == null)
               continue;

            // Note that new Guid() is useless - it creates a GUID of all 0s.
            grid.Name = Guid.NewGuid().ToString();
         }
      
         foreach (IfcGrid grid in Grids)
            grid.CreateGrid(m_Cache);
         foreach (IfcProduct element in Elements.Values)
            element.CreateElement(m_Cache, WorldCoordinateSystem);
      }
   }
}