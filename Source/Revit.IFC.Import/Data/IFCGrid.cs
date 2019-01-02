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
   /// Represents an IfcGrid, which corresponds to a group of Revit Grid elements.
   /// The fields of the IFCGrid class correspond to the IfcGrid entity defined in the IFC schema.
   /// </summary>
   public static class IFCGrid
   {
      /// <summary>
      /// Create either the U, V, or W grid lines.
      /// </summary>
      /// <param name="axes">The list of axes in a particular direction.</param>
      /// <param name="doc">The document.</param>
      /// <param name="lcs">The local transform.</param>
      private static void CreateOneDirection(IList<IfcGridAxis> axes, CreateElementIfcCache cache, Transform lcs, Dictionary<ElementId, IfcPresentationLayerAssignment> presentationLayerAssignmentsForAxes)
      {
         foreach (IfcGridAxis axis in axes)
         {
            if (axis == null)
               continue;

            try
            {
               ElementId createdElementId = axis.CreateGridAxis(cache, lcs);
               if (createdElementId != ElementId.InvalidElementId && axis.AxisCurve != null)
               {
                  IfcPresentationLayerAssignment layerAssignment = axis.AxisCurve.LayerAssignment.FirstOrDefault();
                  if (layerAssignment != null)
                     presentationLayerAssignmentsForAxes[createdElementId] = layerAssignment;
               }
            }
            catch
            {
            }
         }
      }

      /// <summary>
      /// As IfcGrid should have at most one associated IFCPresentationLayerAssignment.  Return it if it exists.
      /// </summary>
      /// <returns>The associated IFCPresentationLayerAssignment, or null.</returns>
      private static IfcPresentationLayerAssignment GetTheFirstPresentationLayerAssignment(this IfcGrid grid)
      {
         IfcProductRepresentation productRepresentation = grid.Representation;
         if (productRepresentation == null)
            return null;

         foreach (IfcRepresentation representation in productRepresentation.Representations)
         {

            foreach (IfcRepresentationItem representationItem in representation.Items)
            {
               // We will favor the layer assignment of the items over the representation itself.
               IfcPresentationLayerAssignment layerAssignment = representationItem.LayerAssignments.FirstOrDefault();
               if (layerAssignment != null)
                  return layerAssignment;
            }

            IfcPresentationLayerAssignment presentationLayerAssignment = representation.LayerAssignments.FirstOrDefault();
            if (presentationLayerAssignment != null)
               return presentationLayerAssignment;
         }

         return null;
      }

      /// <summary>
      /// Override PresentationLayerNames for the current axis.
      /// </summary>
      /// <param name="defaultLayerAssignmentName">The grid's layer assignment name</param>
      /// <param name="hasDefaultLayerAssignmentName">True if defaultLayerAssignmentName isn't empty.</param>
      private static HashSet<string> GetPresentationLayerNames(this IfcGrid grid, string defaultLayerAssignmentName, bool hasDefaultLayerAssignmentName, ElementId createdElementId, Dictionary<ElementId, IfcPresentationLayerAssignment> presentationLayerAssignmentsForAxes)
      {
         HashSet<string> presentationLayerNames = new HashSet<string>();

         // We will get the presentation layer names from either the grid lines or the grid, with
         // grid lines getting the higher priority.
         IfcPresentationLayerAssignment currentLayerAssigment;
         if (presentationLayerAssignmentsForAxes.TryGetValue(createdElementId, out currentLayerAssigment) &&
             currentLayerAssigment != null &&
             !string.IsNullOrWhiteSpace(currentLayerAssigment.Name))
            presentationLayerNames.Add(currentLayerAssigment.Name);
         else if (hasDefaultLayerAssignmentName)
            presentationLayerNames.Add(defaultLayerAssignmentName);
         return presentationLayerNames;
      }

      /// <summary>
      /// Creates or populates Revit elements based on the information contained in this class.
      /// </summary>
      /// <param name="doc">The document.</param>
      internal static void CreateGrid(this IfcGrid grid, CreateElementIfcCache cache)
      {
         Transform lcs = grid.ObjectTransform();

         Dictionary<ElementId, IfcPresentationLayerAssignment> presentationLayerAssignmentsForAxes = new Dictionary<ElementId, IfcPresentationLayerAssignment>();

         CreateOneDirection(grid.UAxes, cache, lcs, presentationLayerAssignmentsForAxes);
         CreateOneDirection(grid.VAxes, cache, lcs, presentationLayerAssignmentsForAxes);
         CreateOneDirection(grid.WAxes, cache, lcs, presentationLayerAssignmentsForAxes);

         ISet<ElementId> createdElementIds = new HashSet<ElementId>();
         grid.GetCreatedElementIds(createdElementIds, cache);

         // We want to get the presentation layer from the Grid representation, if any.
         IfcPresentationLayerAssignment defaultLayerAssignment = grid.GetTheFirstPresentationLayerAssignment();
         string defaultLayerAssignmentName = (defaultLayerAssignment != null) ? defaultLayerAssignment.Name : null;
         bool hasDefaultLayerAssignmentName = !string.IsNullOrWhiteSpace(defaultLayerAssignmentName);

         foreach (ElementId createdElementId in createdElementIds)
         {

            HashSet<string> presentationLayerNames = grid.GetPresentationLayerNames(defaultLayerAssignmentName, hasDefaultLayerAssignmentName, createdElementId, presentationLayerAssignmentsForAxes);
            grid.CreateParameters(cache, createdElementId, presentationLayerNames);
         }
      }
   }
}