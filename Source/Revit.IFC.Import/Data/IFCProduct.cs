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

using TemporaryDisableLogging = Revit.IFC.Import.Utility.IFCImportOptions.TemporaryDisableLogging;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   /// <summary>
   /// Represents an IfcProduct.
   /// </summary>
   public static class IFCProduct
   {
      /// <summary>
      /// The local coordinate system of the IfcProduct.
      /// </summary>
      public static Transform ObjectTransform(this IfcProduct product)
      {
         IfcObjectPlacement placement = product.Placement;
         if (placement == null)
            return Transform.Identity;
         return placement.TotalTransform();
      }

      /// <summary>
      /// Creates or populates Revit element params based on the information contained in this class.
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="element">The element.</param>
      internal static void CreateParametersInternal(this IfcProduct product, Document doc, Element element, HashSet<string> presentationLayerNames)
      {
         if (element != null)
         {
            // Set "IfcPresentationLayer" parameter.
            string ifcPresentationLayer = null;
            if (presentationLayerNames != null)
            {
               foreach (string currLayer in presentationLayerNames)
               {
                  if (string.IsNullOrWhiteSpace(currLayer))
                     continue;

                  if (ifcPresentationLayer == null)
                     ifcPresentationLayer = currLayer;
                  else
                     ifcPresentationLayer += "; " + currLayer;
               }
            }

            if (ifcPresentationLayer != null)
               IFCPropertySet.AddParameterString(doc, element, "IfcPresentationLayer", ifcPresentationLayer, product.StepId);

            // Set the container name of the element.
            IfcRelAggregates relAggregates = product.Decomposes;
            IfcSpatialStructureElement container = relAggregates == null ? null : relAggregates.RelatingObject as IfcSpatialStructureElement;
            string containerName = (container != null) ? container.Name : null;
            if (!string.IsNullOrEmpty(containerName))
               IFCPropertySet.AddParameterString(doc, element, "IfcSpatialContainer", containerName, product.StepId);

            IfcDistributionPort distributionPort = product as IfcDistributionPort;
            if (distributionPort != null)
            {
               IFCPropertySet.AddParameterString(doc, element, "Flow Direction", distributionPort.FlowDirection.ToString(), distributionPort.StepId);
            }
            else
            {
               IfcPort port = product as IfcPort;
               if (port != null)
               {
                  port.CreateParametersInternal(doc, element);
               }
               else
               {
                  IfcProxy proxy = product as IfcProxy;
                  if (proxy != null)
                  {
                     string ifcTag = proxy.Tag;
                     if (!string.IsNullOrWhiteSpace(ifcTag))
                        IFCPropertySet.AddParameterString(doc, element, "IfcTag", ifcTag, proxy.StepId);

                     // Set "ProxyType" parameter.
                     string ifcProxyType = proxy.ProxyType.ToString();
                     if (!string.IsNullOrWhiteSpace(ifcProxyType))
                        IFCPropertySet.AddParameterString(doc, element, "IfcProxyType", ifcProxyType, proxy.StepId);
                  }
                  else
                  {
                     IfcSpatialStructureElement spatialStructureElement = product as IfcSpatialStructureElement;
                     if (spatialStructureElement != null)
                     {
                        string longName = spatialStructureElement.LongName;
                        if (!string.IsNullOrWhiteSpace(longName))
                        {
                           string parameterName = "LongNameOverride";
                           if (element is ProjectInfo)
                              parameterName = spatialStructureElement.StepClassName.ToString() + " " + parameterName;

                           IFCPropertySet.AddParameterString(doc, element, parameterName, longName, spatialStructureElement.StepId);
                        }

                        IfcBuildingStorey buildingStorey = spatialStructureElement as IfcBuildingStorey;
                        if (buildingStorey != null)
                        {
                           if (!double.IsNaN(buildingStorey.Elevation))
                              IFCPropertySet.AddParameterDouble(doc, element, "IfcElevation", UnitType.UT_Length, buildingStorey.Elevation, buildingStorey.StepId);
                        }
                        else
                        {
                           IfcSite site = spatialStructureElement as IfcSite;
                           if (site != null)
                              site.CreateParametersInternal(doc, element);
                        }
                     }
                  }
               }
            }
         }
      }

      /// <summary>
      /// Private function to determine whether an IFCProduct directly contains vaoid geometry.
      /// </summary>
      /// <returns>True if the IFCProduct directly contains valid geometry.</returns>
      private static bool HasValidTopLevelGeometry(this IfcProduct product)
      {
         return (product.Representation != null && product.Representation.IsValid());
      }

      /// <summary>
      /// Private function to determine whether an IFCProduct contins geometry in a sub-element.
      /// </summary>
      /// <param name="visitedEntities">A list of already visited entities, to avoid infinite recursion.</param>
      /// <returns>True if the IFCProduct directly or indirectly contains geometry.</returns>
      private static bool HasValidSubElementGeometry(this IfcProduct product, IList<IfcObjectDefinition> visitedEntities)
      {
         // If the ProductRepresentation doesn't contain valid geometry, then the ComposedObjectDefinitions determine if it has geometry or not.

         if(product.IsDecomposedBy.Count == 0)
            return false;

         foreach (IfcObjectDefinition objectDefinition in product.IsDecomposedBy.SelectMany(x=>x.RelatedObjects))
         {
            if (visitedEntities.Contains(objectDefinition))
               continue;

            visitedEntities.Add(objectDefinition);

            IfcProduct ifcProduct = objectDefinition as IfcProduct; 
            if (ifcProduct == null)
               continue;

            if (ifcProduct.HasValidTopLevelGeometry())
               return true;

            if (ifcProduct.HasValidSubElementGeometry(visitedEntities))
               return true;
         }

         return false;
      }

      /// <summary>
      /// Creates or populates Revit elements based on the information contained in this class.
      /// </summary>
      /// <param name="doc">The document.</param>
      internal static void CreateProduct(this IfcProduct product, CreateElementIfcCache cache, bool createElement, Transform globalTransform)
      {
         IfcDistributionPort distributionPort = product as IfcDistributionPort;
         if (distributionPort != null)
            distributionPort.CreatePort(cache, ElementId.InvalidElementId);

         if (product.HasValidTopLevelGeometry())
         {
            product.ConvertRepresentation(cache, globalTransform, createElement);
         }
         else
         {
            IfcSpatialStructureElement spatialStructureElement = product as IfcSpatialStructureElement;
            if (spatialStructureElement != null)
            {
               IfcBuildingStorey buildingStorey = spatialStructureElement as IfcBuildingStorey;
               if (buildingStorey != null)
                  buildingStorey.Create(cache);
               else if (spatialStructureElement is IfcBuilding || spatialStructureElement is IfcSite)
               {
                  cache.CreatedElements[spatialStructureElement.StepId] = Importer.TheCache.ProjectInformationId;
               }
            }
            else if (product is IfcElement || product is IfcGrid)
            {
               IList<IfcObjectDefinition> visitedEntities = new List<IfcObjectDefinition>();
               visitedEntities.Add(product);
               if (!product.HasValidSubElementGeometry(visitedEntities))
               {
                  Importer.TheLog.LogWarning(product.StepId, "There is no valid geometry for this " + product.StepClassName + "; entity will not be built.", false);
               }
            }
         }
      }

      internal static Tuple<IList<IFCSolidInfo>, IList<Curve>> ConvertRepresentation(this IfcProduct product, CreateElementIfcCache cache, Transform globalTransform, bool createElement)
      {
         return product.ConvertRepresentation(cache, globalTransform, createElement, product);
      }
      internal static Tuple<IList<IFCSolidInfo>, IList<Curve>> ConvertRepresentation(this IfcProduct product, CreateElementIfcCache cache, Transform globalTransform, bool createElement, IfcObjectDefinition creatorOverride)
      {
         IfcObjectDefinition creator = creatorOverride == null ? product : creatorOverride;
         using (IFCImportShapeEditScope shapeEditScope = IFCImportShapeEditScope.Create(cache.Document, creator))
         {
            ElementId graphicsStyleId;
            shapeEditScope.CategoryId = creator.CategoryId(cache, out graphicsStyleId);
            shapeEditScope.GraphicsStyleId = graphicsStyleId;
            // The name can be added as well. but it is usually less useful than 'oid'
            string myId = product.GlobalId; // + "(" + Name + ")";
            List<IFCSolidInfo> voids = new List<IFCSolidInfo>();
            bool preventInstances = product is IfcFeatureElementSubtraction;
            IfcElement element = product as IfcElement;
            if (element != null)
            {
               IfcOpeningElement openingElement = element as IfcOpeningElement;
               if (openingElement != null)
                  preventInstances = true;
               foreach (IfcFeatureElementSubtraction opening in element.HasOpenings.Select(x => x.RelatedOpeningElement))
               {
                  try
                  {
                     preventInstances = true;
                     // Create the actual Revit element based on the IFCFeatureElement here.
                     ElementId openingId = opening.CreateElement(cache, globalTransform);

                     // This gets around the issue that the Boolean operation between the void(s) in the IFCFeatureElement and 
                     // the solid(s) in the IFCElement may use the Graphics Style of the voids in the resulting Solid(s), meaning 
                     // that some faces may disappear when we turn off the visibility of IfcOpeningElements.

                     Tuple<IList<IFCSolidInfo>, IList<Curve>> openingGeometry = opening.ConvertRepresentation(cache, globalTransform, false, creator);
                     voids.AddRange(openingGeometry.Item1);
                  }
                  catch (Exception ex)
                  {
                     Importer.TheLog.LogError(opening.StepId, ex.Message, false);
                  }
               }
            }

            shapeEditScope.PreventInstances = preventInstances;
            Transform lcs = globalTransform.Multiply(product.ObjectTransform());

            product.Representation.CreateProductRepresentation(cache, shapeEditScope, lcs, lcs, myId);
            IList<IFCSolidInfo> solids = shapeEditScope.Solids;
            int numSolids = solids.Count;
            int numVoids = voids.Count;
            if ((numSolids > 0) && (numVoids > 0))
            {
               // Attempt to cut each solid with each void.
               for (int solidIdx = 0; solidIdx < numSolids; solidIdx++)
               {
                  // We only cut body representation items.
                  if (solids[solidIdx].RepresentationType != IFCRepresentationIdentifier.Body)
                     continue;

                  if (!(solids[solidIdx].GeometryObject is Solid))
                  {
                     string typeName = (solids[solidIdx].GeometryObject is Mesh) ? "mesh" : "instance";
                     Importer.TheLog.LogError(product.StepId, "Can't cut " + typeName + " geometry, ignoring " + numVoids + " void(s).", false);
                     continue;
                  }

                  for (int voidIdx = 0; voidIdx < numVoids; voidIdx++)
                  {
                     if (!(voids[voidIdx].GeometryObject is Solid))
                     {
                        Importer.TheLog.LogError(product.StepId, "Can't cut Solid geometry with a Mesh (# " + voids[voidIdx].Id + "), ignoring.", false);
                        continue;
                     }

                     solids[solidIdx].GeometryObject =
                         IFCGeometryUtil.ExecuteSafeBooleanOperation(solids[solidIdx].Id, voids[voidIdx].Id,
                             solids[solidIdx].GeometryObject as Solid, voids[voidIdx].GeometryObject as Solid,
                             BooleanOperationsType.Difference, null);
                     if ((solids[solidIdx].GeometryObject as Solid).Faces.IsEmpty)
                     {
                        solids.RemoveAt(solidIdx);
                        solidIdx--;
                        numSolids--;
                        break;
                     }
                  }
               }
            }

            bool addedCurves = shapeEditScope.AddPlaneViewCurves(product.StepId);
            if (createElement)
            {
               if (solids.Count > 0 || addedCurves)
               {
                  DirectShape shape = Importer.TheCache.UseElementByGUID<DirectShape>(cache.Document, product.GlobalId);

                  if (shape == null)
                     shape = IFCElementUtil.CreateElement(cache, product.CategoryId(cache), product.GlobalId, null, product.StepId);

                  List<GeometryObject> directShapeGeometries = new List<GeometryObject>();
                  foreach (IFCSolidInfo geometryObject in solids)
                  {
                     // We need to check if the solid created is good enough for DirectShape.  If not, warn and use a fallback Mesh.
                     GeometryObject currObject = geometryObject.GeometryObject;
                     if (currObject is Solid)
                     {
                        Solid solid = currObject as Solid;
                        if (!shape.IsValidGeometry(solid))
                        {
                           Importer.TheLog.LogWarning(product.StepId, "Couldn't create valid solid, reverting to mesh.", false);
                           directShapeGeometries.AddRange(IFCGeometryUtil.CreateMeshesFromSolid(solid));
                           currObject = null;
                        }
                     }

                     if (currObject != null)
                        directShapeGeometries.Add(currObject);
                  }

                  // We will use the first IfcTypeObject id, if it exists.  In general, there should be 0 or 1.
                  ElementId typeId = ElementId.InvalidElementId;
                  IfcTypeObject typeObject = product.RelatingType();
                  if (typeObject != null)
                  {
                     if (!cache.CreatedElements.TryGetValue(typeObject.StepId, out typeId))
                        typeId = ElementId.InvalidElementId;
                  }

                  shape.SetShape(directShapeGeometries);
                  shapeEditScope.SetPlanViewRep(shape);

                  if (typeId != ElementId.InvalidElementId)
                     shape.SetTypeId(typeId);

                  //PresentationLayerNames.UnionWith(shapeEditScope.PresentationLayerNames);

               }
            }
            return new Tuple<IList<IFCSolidInfo>, IList<Curve>>(shapeEditScope.Solids, shapeEditScope.FootPrintCurves);
         }
      }

      
   }
}