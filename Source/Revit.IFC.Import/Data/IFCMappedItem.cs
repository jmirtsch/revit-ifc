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
using Revit.IFC.Import.Geometry;
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   public static class IFCMappedItem
   {
      /// <summary>
      /// Create geometry for a particular representation item.
      /// </summary>
      /// <param name="shapeEditScope">The geometry creation scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      internal static void CreateShapeMappedItem(this IfcMappedItem mappedItem, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         Transform mappingTarget = mappedItem.MappingTarget.GetTransform();
         // Check scale; if it is uniform, create an instance.  If not, create a shape directly.
         // TODO: Instead allow creation of instances based on similar scaling.
         double scaleX = mappingTarget.Scale;
         double scaleY = scaleX, scaleZ = scaleX;
         IfcCartesianTransformationOperator2DnonUniform cartesianTransformationOperator2DnonUniform = mappedItem.MappingTarget as IfcCartesianTransformationOperator2DnonUniform;
         if (cartesianTransformationOperator2DnonUniform != null)
            scaleY = cartesianTransformationOperator2DnonUniform.Scale2;
         else
         {
            IfcCartesianTransformationOperator3DnonUniform cartesianTransformationOperator3DnonUniform = mappedItem.MappingTarget as IfcCartesianTransformationOperator3DnonUniform;
            if(cartesianTransformationOperator3DnonUniform != null)
            {
               scaleY = cartesianTransformationOperator3DnonUniform.Scale2;
               scaleZ = cartesianTransformationOperator3DnonUniform.Scale3;
            }
         }
         bool isUnitScale = (MathUtil.IsAlmostEqual(scaleX, 1.0) &&
             MathUtil.IsAlmostEqual(scaleY, 1.0) &&
             MathUtil.IsAlmostEqual(scaleZ, 1.0));

         Transform newLcs = null;
         if (lcs == null)
            newLcs = mappingTarget;
         else if (mappingTarget == null)
            newLcs = lcs;
         else
            newLcs = lcs.Multiply(mappingTarget);

         Transform newScaledLcs = null;
         if (scaledLcs == null)
            newScaledLcs = mappingTarget;
         else if (mappingTarget == null)
            newScaledLcs = scaledLcs;
         else
            newScaledLcs = scaledLcs.Multiply(mappingTarget);

         // Pass in newLCS = null, use newLCS for instance.
         bool isFootprint = (string.Compare(shapeEditScope.ContainingRepresentation.RepresentationIdentifier, IFCRepresentationIdentifier.FootPrint.ToString(),true) == 0);

         bool canCreateType = !shapeEditScope.PreventInstances && 
            (newLcs != null && newLcs.IsConformal) &&
            (newScaledLcs != null && newScaledLcs.IsConformal) &&
            isUnitScale &&
            (shapeEditScope.ContainingRepresentation != null && !isFootprint);

         if (canCreateType)
         {
            mappedItem.MappingSource.CreateShapeRepresentationMap(cache, shapeEditScope, null, null, guid);
            IList<GeometryObject> instances = DirectShape.CreateGeometryInstance(shapeEditScope.Document, mappedItem.MappingSource.StepId.ToString(), newLcs);
            foreach (GeometryObject instance in instances)
               shapeEditScope.Solids.Add(IFCSolidInfo.Create(mappedItem.StepId, instance));
         }
         else
         {
            if (!isUnitScale)
            {
               XYZ xScale = new XYZ(scaleX, 0.0, 0.0);
               XYZ yScale = new XYZ(0.0, scaleY, 0.0);
               XYZ zScale = new XYZ(0.0, 0.0, scaleZ);
               Transform scaleTransform = Transform.Identity;
               scaleTransform.set_Basis(0, xScale);
               scaleTransform.set_Basis(1, yScale);
               scaleTransform.set_Basis(2, zScale);
               newScaledLcs = newScaledLcs.Multiply(scaleTransform);
            }

            mappedItem.MappingSource.CreateShapeRepresentationMap(cache, shapeEditScope, newLcs, newScaledLcs, guid);
         }
      }

   }
} 