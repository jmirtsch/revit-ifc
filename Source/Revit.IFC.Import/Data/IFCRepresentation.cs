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
   public enum IFCRepresentationIdentifier
   {
      Axis,
      Body,
      Box,
      FootPrint,
      Style,
      Unhandled
   }

   /// <summary>
   /// Represents an IfcRepresentation.
   /// </summary>
   public static class IFCRepresentation
   {
      private static bool NotAllowedInRepresentation(this IfcRepresentation representation, IfcRepresentationItem item)
      {
         IFCRepresentationIdentifier identifier;
         if (Enum.TryParse<IFCRepresentationIdentifier>(representation.RepresentationIdentifier, out identifier))
         {
            switch (identifier)
            {
               case IFCRepresentationIdentifier.Axis:
                  return !(item is IfcCurve || item is IfcMappedItem);
               case IFCRepresentationIdentifier.Body:
                  return false;
               case IFCRepresentationIdentifier.Box:
                  return !(item is IfcBoundingBox);
               case IFCRepresentationIdentifier.FootPrint:
                  return !(item is IfcCurve || item is IfcGeometricSet || item is IfcMappedItem);
               case IFCRepresentationIdentifier.Style:
                  return !(item is IfcStyledItem);
            }
         }

         return false;
      }

      private static void CreateBoxShape(this IfcRepresentation representation, IfcBoundingBox boundingBox, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform scaledLcs)
      {
         using (IFCImportShapeEditScope.IFCContainingRepresentationSetter repSetter = new IFCImportShapeEditScope.IFCContainingRepresentationSetter(shapeEditScope, representation))
         {
            // Get the material and graphics style based in the "Box" sub-category of Generic Models.  
            // We will create the sub-category if this is our first time trying to use it.
            // Note that all bounding boxes are controlled by a sub-category of Generic Models.  We may revisit that decision later.
            // Note that we hard-wire the identifier to "Box" because older files may have bounding box items in an obsolete representation.
            SolidOptions solidOptions = null;
            Category bboxCategory = IFCCategoryUtil.GetSubCategoryForRepresentation(cache, representation.StepId, IFCRepresentationIdentifier.Box);
            if (bboxCategory != null)
            {
               ElementId materialId = (bboxCategory.Material == null) ? ElementId.InvalidElementId : bboxCategory.Material.Id;
               GraphicsStyle graphicsStyle = bboxCategory.GetGraphicsStyle(GraphicsStyleType.Projection);
               ElementId gstyleId = (graphicsStyle == null) ? ElementId.InvalidElementId : graphicsStyle.Id;
               solidOptions = new SolidOptions(materialId, gstyleId);
            }
         
            Solid bboxSolid = IFCGeometryUtil.CreateSolidFromBoundingBox(scaledLcs, ProcessBoundingBox(boundingBox), solidOptions);
            if (bboxSolid != null)
            {
               IFCSolidInfo bboxSolidInfo = IFCSolidInfo.Create(representation.StepId, bboxSolid);
               shapeEditScope.Solids.Add(bboxSolidInfo);
            }
         }
         return;
      }

      /// <summary>
      /// Create geometry for a particular representation.
      /// </summary>
      /// <param name="shapeEditScope">The geometry creation scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      public static void CreateShape(this IfcRepresentation representation, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         // Special handling for Box representation.  We may decide to create an IFCBoundingBox class and stop this special treatment.
         List<IfcBoundingBox> boundingBoxes = representation.Items.OfType<IfcBoundingBox>().ToList();
         if (boundingBoxes.Count > 0)
         {
            representation.CreateBoxShape(boundingBoxes[0], cache, shapeEditScope, scaledLcs);
            if (boundingBoxes.Count > 1)
            {
               Importer.TheLog.LogWarning(representation.StepId, "Found multiple IfcBoundingBox representation item, ignoring.", false);
            }
         }
         IfcPresentationLayerAssignment layerAssignment = representation.LayerAssignments.FirstOrDefault();
         if (layerAssignment != null)
            layerAssignment.CreatePresentationLayerAssignment(shapeEditScope, cache);
         using (IFCImportShapeEditScope.IFCMaterialStack stack = new IFCImportShapeEditScope.IFCMaterialStack(shapeEditScope, cache, null, layerAssignment))
         {
            using (IFCImportShapeEditScope.IFCContainingRepresentationSetter repSetter = new IFCImportShapeEditScope.IFCContainingRepresentationSetter(shapeEditScope, representation))
            {
               foreach (IfcRepresentationItem item in representation.Items)
               {
                  if (representation.NotAllowedInRepresentation(item))
                  {
                     Importer.TheLog.LogWarning(item.StepId, "Ignoring unhandled representation item of type " + item.StepClassName + " in " +
                         representation.RepresentationIdentifier + " representation.", true);
                     continue;
                  }
                  item.CreateShapeItem(cache, shapeEditScope, lcs, scaledLcs, guid);
               }
            }
         }

      }

      static private BoundingBoxXYZ ProcessBoundingBox(IfcBoundingBox boundingBoxHnd)
      {
         IfcCartesianPoint lowerLeftHnd = boundingBoxHnd.Corner;
         XYZ minXYZ = IFCPoint.ProcessScaledLengthIFCCartesianPoint(lowerLeftHnd);

         double xDim = IFCUnitUtil.ScaleLength(boundingBoxHnd.XDim);

         double yDim = IFCUnitUtil.ScaleLength(boundingBoxHnd.YDim);

         double zDim = IFCUnitUtil.ScaleLength(boundingBoxHnd.ZDim);

         XYZ maxXYZ = new XYZ(minXYZ.X + xDim, minXYZ.Y + yDim, minXYZ.Z + zDim);
         BoundingBoxXYZ boundingBox = new BoundingBoxXYZ();
         boundingBox.set_Bounds(0, minXYZ);
         boundingBox.set_Bounds(1, maxXYZ);
         return boundingBox;
      }

   } 
}