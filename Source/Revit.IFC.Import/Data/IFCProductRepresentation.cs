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
   /// Represents IfcProductRepresentation and IfcMaterialDefinitionRepresentation.
   /// </summary>
   public static class IFCProductRepresentation
   {
      /// <summary>
      /// Returns true if there is anything to create.
      /// </summary>
      /// <returns>Returns true if there is anything to create, false otherwise.</returns>
      public static bool IsValid(this IfcProductRepresentation productRepresentation)
      {
         // TODO: We are not creating a shape if there is no representation for the shape.  We may allow this for specific entity types,
         // such as doors or windows.
         return (productRepresentation.Representations != null && productRepresentation.Representations.Count != 0);
      }

      

      internal static List<IfcRepresentation> SortedRepresentations(this IfcProductRepresentation productRepresentation)
      {
         // Partially sort the representations so that we create: Body, Box, then the rest of the representations in that order.
         // This allows us to skip Box representations if any of the Body representations create 3D geometry.  Until we have UI in place, 
         // this will disable creating extra 3D (bounding box) geometry that clutters the display, is only marginally useful and is hard to turn off.
         List<IfcRepresentation> sortedReps = new List<IfcRepresentation>(); // Double usage as body rep list.
         IList<IfcRepresentation> boxReps = new List<IfcRepresentation>();
         IList<IfcRepresentation> otherReps = new List<IfcRepresentation>();

         foreach (IfcRepresentation representation in productRepresentation.Representations)
         {
            IFCRepresentationIdentifier representationIdentifier = IFCRepresentationIdentifier.Unhandled;
            if (!Enum.TryParse<IFCRepresentationIdentifier>(representation.RepresentationIdentifier, out representationIdentifier))
               representationIdentifier = IFCRepresentationIdentifier.Unhandled;
            switch (representationIdentifier)
            {
               case IFCRepresentationIdentifier.Body:
                  sortedReps.Add(representation);
                  break;
               case IFCRepresentationIdentifier.Box:
                  boxReps.Add(representation);
                  break;
               default:
                  otherReps.Add(representation);
                  break;
            }
         }

         // Add back the other representations.
         sortedReps.AddRange(boxReps);
         sortedReps.AddRange(otherReps);

         return sortedReps;
      }
      /// <summary>
      /// Creates or populates Revit elements based on the information contained in this class.
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="lcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      public static void CreateProductRepresentation(this IfcProductRepresentation productRepresentation, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         List<IfcRepresentation> sortedReps = productRepresentation.SortedRepresentations();
         foreach (IfcRepresentation representation in sortedReps)
         {
            // Since we process all Body representations first, the misnamed "Solids" field will contain 3D geometry.
            // If this isn't empty, then we'll skip the bounding box, unless we are always importing bounding box geometry.
            // Note that we process Axis representations later since they create model geometry also,
            // but we don't consider Axis or 2D geometry in our decision to import bounding boxes.  
            // Note also that we will only read in the first bounding box, which is the maximum of Box representations allowed.
            if (string.Compare(representation.RepresentationIdentifier, IFCRepresentationIdentifier.Box.ToString(),true) == 0 &&
               IFCImportFile.TheFile.Options.ProcessBoundingBoxGeometry != IFCProcessBBoxOptions.Always && shapeEditScope.Solids.Count > 0)
               continue;

           representation.CreateShape(cache, shapeEditScope, lcs, scaledLcs, guid);
         }
      }

      /// <summary>
      /// Gets the IFCSurfaceStyle, if available, for an associated material.
      /// TODO: Make this generic, add warnings.
      /// </summary>
      /// <returns>The IFCSurfaceStyle.</returns>
      public static IfcSurfaceStyle GetSurfaceStyle(this IfcProductRepresentation productRepresentation)
      {
         IList<IfcRepresentation> representations = productRepresentation.Representations;
         if (representations != null && representations.Count > 0)
         {
            IfcRepresentation representation = representations[0];
            IfcStyledItem styledItem = representation.Items.OfType<IfcStyledItem>().FirstOrDefault();
            if (styledItem != null)
            {
               return styledItem.GetSurfaceStyle();
            }
         }
         return null;
      }
   }
}