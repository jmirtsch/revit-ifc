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
using Revit.IFC.Import.Data;
using Revit.IFC.Import.Enums;
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   /// <summary>
   /// Represents an IFC object definition.
   /// </summary>
   public static class IFCObjectDefinition
   {
      public static ElementId CategoryId(this IfcObjectDefinition objectDefinition, CreateElementIfcCache cache)
      {
         ElementId graphicStyleId;
         return objectDefinition.CategoryId(cache, out graphicStyleId);
      }
      /// <summary>
      /// The category id corresponding to the element created for this IFCObjectDefinition.
      /// </summary>
      public static ElementId CategoryId(this IfcObjectDefinition objectDefinition, CreateElementIfcCache cache, out ElementId graphicStyleId)
      {
         return IFCCategoryUtil.GetCategoryIdForEntity(cache, objectDefinition, out graphicStyleId);
      }

      /// <summary>
      /// Returns true if sub-elements should be grouped; false otherwise.
      /// </summary>
      public static bool GroupSubElements(this IfcObjectDefinition objectDefinition)
      {
         IfcSpatialStructureElement spatialStructureElement = objectDefinition as IfcSpatialStructureElement;
         if (spatialStructureElement != null)
            return false;
         IfcProject project = objectDefinition as IfcProject;
         if (project != null)
            return false;
         return true;
      }

      /// <summary>
      /// The list of materials directly associated with the element.  There may be more at the type level.
      /// </summary>
      /// <returns>A list, possibly empty, of materials directly associated with the element.</returns>
      public static IList<IfcMaterial> GetMaterials(this IfcObjectDefinition objectDefinition)
      {
         IList<IfcMaterial> materials = null;
         IfcRelAssociatesMaterial relAssociatesMaterial = objectDefinition.HasAssociations.OfType<IfcRelAssociatesMaterial>().FirstOrDefault();
         if(relAssociatesMaterial != null)
            materials = relAssociatesMaterial.RelatingMaterial.GetMaterials();

         if (materials == null)
            return new List<IfcMaterial>();

         return materials;
      }
      public static IfcMaterialSelect MaterialSelect(this IfcObjectDefinition objectDefinition)
      {
         IfcRelAssociatesMaterial relAssociatesMaterial = objectDefinition.HasAssociations.OfType<IfcRelAssociatesMaterial>().FirstOrDefault();
         if (relAssociatesMaterial == null)
            return null;
         return relAssociatesMaterial.RelatingMaterial;
      }
      /// <summary>
      /// Return the materials' names and thicknesses if the object is created with IFCMaterialLayerSetUsage information.
      /// The thickness is returned as a string followed by its unit
      /// If the object is not created with IFCMaterialLayerSetUsage information, then only the materials' names are returned
      /// </summary>
      /// <returns>A list in which each entry is the material's names followed by their thicknesses if the thicknesses are available</returns>
      public static IList<string> GetMaterialsNamesAndThicknesses(this IfcObjectDefinition objectDefinition)
      {
         IList<string> result = new List<string>();

         string thickness = null;
         string name = null;
         // If this object is created with IFCMaterialLayerSetUsage information 
         // then the material layer thickness will be added after the name of each layer.
         IfcMaterialSelect materialSelect = objectDefinition.MaterialSelect();
         if (materialSelect == null)
            return result;
         IfcMaterialLayerSetUsage materialLayerSetUsage = materialSelect as IfcMaterialLayerSetUsage;
         if (materialLayerSetUsage != null)
         {
            IList<IfcMaterialLayer> materialLayers = materialLayerSetUsage.ForLayerSet.MaterialLayers;
            IfcMaterial material;

            foreach (IfcMaterialLayer materialLayer in materialLayers)
            {
               if (materialLayer == null)
                  continue;
               material = materialLayer.Material;
               if (material == null || string.IsNullOrWhiteSpace(material.Name))
                  continue;
               name = material.Name;
               thickness = IFCUnitUtil.FormatLengthAsString(materialLayer.LayerThickness);
               result.Add(name + ": " + thickness);
            }
         }
         else
         {
            IfcMaterialProfileSetUsage materialProfileSetUsage = materialSelect as IfcMaterialProfileSetUsage;
            if (materialProfileSetUsage != null)
            {
               IfcMaterialProfileSet materialProfileSet = materialProfileSetUsage.ForProfileSet;
               IList<IfcMaterialProfile> materialProfiles = materialProfileSet.MaterialProfiles;
               IfcMaterial material;

               foreach (IfcMaterialProfile materialProfile in materialProfiles)
               {
                  if (materialProfile == null)
                     continue;   // Skip if it is null
                  material = materialProfile.Material;
                  IfcProfileDef profile = materialProfile.Profile;
                  if (material == null)
                     continue;
                  name = material.Name;
                  string profileName;
                  if (profile != null)
                     profileName = profile.ProfileName;
                  else
                     profileName = profile.ProfileType.ToString();
                  result.Add(name + " (" + profileName + ")");
               }
            }
            else
            {
               IList<IfcMaterial> materials = objectDefinition.GetMaterials();
               foreach (IfcMaterial material in materials)
               {
                  name = material.Name;
                  if (string.IsNullOrWhiteSpace(name))
                     continue;

                  result.Add(name);
               }
            }
         }

         return result;
      }

      /// <summary>
      /// Gets the one material associated with this object.
      /// </summary>
      /// <returns>The material, if there is identically one; otherwise, null.</returns>
      public static IfcMaterial GetTheMaterial(this IfcObjectDefinition objectDefinition)
      {
         IList<IfcMaterial> materials = objectDefinition.GetMaterials();

         IfcMaterial theMaterial = null;
         if (materials.Count > 1)
            return null;

         if (materials.Count == 1)
            theMaterial = materials[0];

         IfcObject obj = objectDefinition as IfcObject;
         if (obj != null)
         {
            IfcTypeObject typeObject = obj.RelatingType();
            if(typeObject != null)
            {
               IList<IfcMaterial> typeMaterials = typeObject.GetMaterials();

               if (typeMaterials.Count > 1)
                  return null;

               if (typeMaterials.Count == 1)
               {
                  if (theMaterial != null && theMaterial.StepId != typeMaterials[0].StepId)
                     return null;
                  theMaterial = typeMaterials[0];
               }
            }
         }
         return theMaterial;
      }

      

      /// <summary>
      /// Creates or populates Revit elements based on the information contained in this class.
      /// </summary>
      /// <param name="doc">The document.</param>
      internal static void CreateSubElements(this IfcObjectDefinition objectDefinition, CreateElementIfcCache cache, Transform globalTransform)
      {
         if (!objectDefinition.GroupSubElements())
            return;

         IList<ElementId> subElementIds = new List<ElementId>();

         // These two should only be populated if GroupSubElements() is true and we are duplicating geometry for containers.
         List<GeometryObject> groupedSubElementGeometries = new List<GeometryObject>();
         List<Curve> groupedSubElementFootprintCurves = new List<Curve>();
         List<IfcBuildingElementPart> parts = new List<IfcBuildingElementPart>();

         foreach(IfcObjectDefinition subObject in objectDefinition.IsDecomposedBy.SelectMany(x=>x.RelatedObjects))
         {
            //// In the case of IFCBuildingElementPart, we want the container to have a copy of the geometry in the category of the parent,
            //// as otherwise the geometry will be controlled by default by the Parts category instead of the parent category.
            IfcBuildingElementPart buildingElementPart = subObject as IfcBuildingElementPart;
            if (buildingElementPart != null)
            {
               parts.Add(buildingElementPart);
               Tuple<IList<IFCSolidInfo>, IList<Curve>> geometry = buildingElementPart.ConvertRepresentation(cache, globalTransform, false, objectDefinition);
               groupedSubElementGeometries.AddRange(geometry.Item1.Select(x => x.GeometryObject));
               groupedSubElementFootprintCurves.AddRange(geometry.Item2);
            }
            else
            {
               ElementId createdElementId = subObject.CreateElement(cache, globalTransform);
               if (createdElementId != ElementId.InvalidElementId)
               {
                  subElementIds.Add(createdElementId);

                  // CreateDuplicateContainerGeometry is currently an API-only option (no UI), set to true by default.
                  if (Importer.TheOptions.CreateDuplicateContainerGeometry)
                  {
                     IfcProduct product = subObject as IfcProduct;

                     if (product != null)
                     {
                        Tuple<IList<IFCSolidInfo>, IList<Curve>> geometry = product.ConvertRepresentation(cache, globalTransform, false, objectDefinition);
                        groupedSubElementGeometries.AddRange(geometry.Item1.Select(x => x.GeometryObject));
                        groupedSubElementFootprintCurves.AddRange(geometry.Item2);
                     }
                  }
               }
            }
         }

         if (subElementIds.Count > 0)  
         {
            ElementId elementId;
            if (cache.CreatedElements.TryGetValue(objectDefinition.StepId, out elementId) && elementId != ElementId.InvalidElementId)
               subElementIds.Add(elementId);
         }
         if(groupedSubElementGeometries.Count > 0 || groupedSubElementFootprintCurves.Count > 0)
         { 
            // We aren't yet actually grouping the elements.  DirectShape doesn't support grouping, and
            // the Group element doesn't support adding parameters.  For now, we will create a DirectShape that "forgets"
            // the association, which is good enough for link.
            DirectShape directShape = IFCElementUtil.CreateElement(cache, objectDefinition.CategoryId(cache), objectDefinition.GlobalId, groupedSubElementGeometries, objectDefinition.StepId);
            //Group group = doc.Create.NewGroup(subElementIds);

            ElementId createdElementId;
            if (directShape != null)
            {
               createdElementId = directShape.Id;

               if (groupedSubElementFootprintCurves.Count != 0 && objectDefinition is IfcProduct)
               {
                  using (IFCImportShapeEditScope planViewScope = IFCImportShapeEditScope.Create(cache.Document, objectDefinition as IfcProduct))
                  {
                     planViewScope.AddPlaneViewCurves(groupedSubElementFootprintCurves, objectDefinition.StepId);
                     planViewScope.SetPlanViewRep(directShape);
                  }
               }
               foreach (IfcBuildingElementPart part in parts)
                  cache.CreatedElements[part.StepId] = createdElementId;
            }
            else
               Importer.TheLog.LogCreationError(objectDefinition, null, false);
         }
      }

      /// <summary>
      /// Processes IfcObjectDefinition attributes.
      /// </summary>
      /// <param name="ifcObjectDefinition">The IfcObjectDefinition handle.</param>
      internal static bool AddToAggregate(this IfcObjectDefinition objectDefinition, ProjectAggregate aggregate, CreateElementIfcCache cache)
      {
         // If we aren't importing this category, skip processing. 
         IfcObject ifcObject = objectDefinition as IfcObject;
         IfcSpatialElement spatialElement = objectDefinition as IfcSpatialElement;
         IfcProduct product = objectDefinition as IfcProduct;
         string predefinedType = ifcObject == null ? objectDefinition.GetPredefinedType() : ifcObject.GetPredefinedType(true);
         IFCEntityType ifcEntityType; // Todo Refactor out the manual listing
         if (Enum.TryParse<IFCEntityType>(objectDefinition.StepClassName, out ifcEntityType))
         {
            if (IFCCategoryUtil.CanImport(ifcEntityType, predefinedType))
            {
               if(product != null)
               {
                  if (spatialElement != null)
                  {
                     IfcBuildingStorey buildingStorey = spatialElement as IfcBuildingStorey;
                     if (buildingStorey != null)
                     {
                        aggregate.Stories.Add(buildingStorey);
                     }
                     else
                     {
                        IfcSpace space = spatialElement as IfcSpace;
                        if (space != null)
                        {
                           aggregate.Spaces.Add(space);
                        }
                        else
                           aggregate.Elements[product.StepId] = product;
                     }
                  }
                  else
                  {
                     IfcGrid grid = objectDefinition as IfcGrid;
                     if (grid != null)
                     {
                        aggregate.Grids.Add(grid);
                        foreach (IfcGridAxis gridAxis in grid.UAxes)
                           gridAxis.AddGridToAggregate(aggregate, cache);
                        foreach (IfcGridAxis gridAxis in grid.VAxes)
                           gridAxis.AddGridToAggregate(aggregate, cache);
                        foreach (IfcGridAxis gridAxis in grid.WAxes)
                           gridAxis.AddGridToAggregate(aggregate, cache);
                     }
                     else
                     {
                        aggregate.Elements[product.StepId] = product;
                        Importer.TheLog.AddToElementCount();
                     }
                  }
               }
               else
               {
                  IfcZone zone = objectDefinition as IfcZone;
                  if (zone != null)
                     aggregate.Zones.Add(zone);
               }
            }
         }
         else
            Importer.TheLog.LogUnhandledSubTypeError(objectDefinition, IFCEntityType.IfcObjectDefinition, false);



         IfcLocalPlacement localPlacement = null;
         if (product != null)
            localPlacement = product.ObjectPlacement as IfcLocalPlacement;
         bool canRemoveSitePlacement = true;
         foreach (IfcObjectDefinition subObject in objectDefinition.IsDecomposedBy.SelectMany(x => x.RelatedObjects))
         {
            if(product != null)
            {
               if(localPlacement != null)
               {
                  IfcProduct subProduct = subObject as IfcProduct;
                  if (subProduct != null)
                  {
                     IfcLocalPlacement subPlacement = subProduct.ObjectPlacement as IfcLocalPlacement;
                     if (subPlacement != null && subPlacement.PlacementRelTo != localPlacement)
                        canRemoveSitePlacement = false;
                  }
               }
            }
            subObject.AddToAggregate(aggregate, cache);
         }
         if(spatialElement != null)
         {
            foreach (IfcProduct subObject in spatialElement.ContainsElements.SelectMany(x => x.RelatedElements))
            {
               if (localPlacement != null)
               {
                  IfcLocalPlacement subPlacement = subObject.ObjectPlacement as IfcLocalPlacement;
                  if (subPlacement != null && subPlacement.PlacementRelTo != localPlacement)
                     canRemoveSitePlacement = false;
               }
               subObject.AddToAggregate(aggregate, cache);
            }
         }
         else
         {
            IfcElement ifcElement = objectDefinition as IfcElement;
            if(ifcElement != null)
            {
               foreach (IfcPort port in ifcElement.HasPortsSS.Select(x => x.RelatingPort))
                  port.AddToAggregate(aggregate, cache);
            }
         }
         return canRemoveSitePlacement;
      }

      /// <summary>
      /// Generates the name for the element to be created.
      /// </summary>
      /// <param name="baseName">If not null, generates a name if Name is invalid.</param>
      /// <returns>The name.</returns>
      internal static string GetName(this IfcObjectDefinition objectDefinition, string baseName)
      {
         if (string.IsNullOrWhiteSpace(objectDefinition.Name))
         {
            if (!string.IsNullOrWhiteSpace(baseName))
               return baseName + " " + objectDefinition.StepId;
            return null;
         }

         return IFCNamingUtil.CleanIFCName(objectDefinition.Name);
      }

      /// <summary>
      /// Generates a valid name for a DirectShapeType associated with this IFCObjectDefinition.
      /// </summary>
      /// <returns></returns>
      public static string GetVisibleName(this IfcObjectDefinition objectDefinition)
      {
         return objectDefinition.GetName("DirectShapeType");
      }

      // In general, we want every created element to have the Element.Name propery set.
      // The list below corresponds of element types where the name is not set by the IFCObjectDefinition directly, 
      // but instead by some other mechanism.
      private static bool CanSetRevitName(Element element)
      {
         // Grids have their name set by IFCGridAxis, which does not inherit from IfcObjectDefinition.
         return (element != null && !(element is Grid) && !(element is ProjectInfo));
      }

      /// <summary>
      /// Allow for override of IfcObjectDefinition shared parameter names.
      /// </summary>
      /// <param name="name">The enum corresponding of the shared parameter.</param>
      /// <returns>The name appropriate for this IfcObjectDefinition.</returns>
      public static string GetSharedParameterName(this IfcObjectDefinition objectDefinition, IFCSharedParameters name)
      {
         if(objectDefinition is IfcBuilding || objectDefinition is IfcGrid || objectDefinition is IfcProject || objectDefinition is IfcSite )
         {
            switch (name)
            {
               case IFCSharedParameters.IfcName:
                  return objectDefinition.StepClassName + " Name";
               case IFCSharedParameters.IfcDescription:
                  return objectDefinition.StepClassName + " Description";
            }
         }
         
         return name.ToString();
      }

      /// <summary>
      /// Set the Element.Name property if possible, and add an "IfcName" parameter to an element containing the original name of the generating entity. 
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="element">The created element.</param>
      private static void SetName(this IfcObjectDefinition objectDefinition, Document doc, Element element)
      {
         string revitName = objectDefinition.GetName(null);
         if (!string.IsNullOrWhiteSpace(revitName))
         {
            try
            {
               if (CanSetRevitName(element))
                  element.Name = revitName;
            }
            catch
            {
            }
         }

         string name = string.IsNullOrWhiteSpace(objectDefinition.Name) ? "" : objectDefinition.Name;
         // 2015: Revit links don't show the name of a selected item inside the link.
         // 2015: DirectShapes don't have a built-in "Name" parameter.
         IFCPropertySet.AddParameterString(doc, element, objectDefinition, IFCSharedParameters.IfcName, objectDefinition.Name, objectDefinition.StepId);
      }

      /// <summary>
      /// Add a parameter "IfcDescription" to an element containing the description of the generating entity. 
      /// If the element has the built-in parameter ALL_MODEL_DESCRIPTION, populate that also.
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="element">The created parameter.</param>
      private static void SetDescription(this IfcObjectDefinition objectDefinition, Document doc, Element element)
      {
         // If the element has the built-in ALL_MODEL_DESCRIPTION parameter, populate that also.
         // We will create/populate the parameter even if the description is empty or null.
         string description = string.IsNullOrWhiteSpace(objectDefinition.Description) ? "" : objectDefinition.Description;
         Parameter descriptionParameter = element.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION);
         if (descriptionParameter != null)
            descriptionParameter.SetValueString(description);
         IFCPropertySet.AddParameterString(doc, element, objectDefinition, IFCSharedParameters.IfcDescription, description, objectDefinition.StepId);
      }

      /// <summary>
      /// Add a parameter "IfcMaterial" to an element containing the name(s) of the materials of the generating entity. 
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="element">The created element.</param>
      /// <remarks>Note that this field contains the names of the materials, and as such is not parametric in any way.</remarks>
      internal static void SetMaterialParameter(this IfcObjectDefinition objectDefinition, Document doc, Element element)
      {
         string materialNames = null;

         IList<string> materialsAndThickness = objectDefinition.GetMaterialsNamesAndThicknesses();
         foreach (string val in materialsAndThickness)
         {
            if (materialNames == null)
               materialNames = val;
            else
               materialNames += ";" + val;
         }
         if (materialNames != null)
            IFCPropertySet.AddParameterString(doc, element, objectDefinition, IFCSharedParameters.IfcMaterial, materialNames, objectDefinition.StepId);
      }

      /// <summary>
      /// Add parameter "IfcSystem" to an element containing the name(s) of the system(s) of the generating entity. 
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="element">The created element.</param>
      /// <remarks>Note that this field contains the names of the systems, and as such is not parametric in any way.</remarks>
      private static void SetSystemParameter(this IfcObjectDefinition objectDefinition, Document doc, Element element)
      {
         string systemNames = null;

         foreach (IfcSystem system in objectDefinition.HasAssignments.OfType<IfcRelAssignsToGroup>().Select(x=>x.RelatingGroup).OfType<IfcSystem>())
         {
            string name = system.Name;
            if (string.IsNullOrWhiteSpace(name))
               continue;

            if (systemNames == null)
               systemNames = name;
            else
               systemNames += ";" + name;
         }

         if (systemNames != null)
            IFCPropertySet.AddParameterString(doc, element, "IfcSystem", systemNames, objectDefinition.StepId);
      }

      /// <summary>
      /// Create property sets for a given element.
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="element">The element being created.</param>
      /// <param name="propertySetsCreated">A concatenated string of property sets created, used to filter schedules.</returns>
      public static void CreatePropertySets(this IfcObjectDefinition objectDefinition, CreateElementIfcCache cache, Element element, string propertySetsCreated)
      {
         IfcObject ifcObject = objectDefinition as IfcObject;
         if (ifcObject != null)
            objectDefinition.CreatePropertySetsBase(cache, element, propertySetsCreated, "IfcPropertySetList", ifcObject.IsDefinedBy.Select(x => x.RelatingPropertyDefinition).ToList());
         else
         {
            IfcTypeObject typeObject = objectDefinition as IfcTypeObject;
            if (typeObject != null)
               typeObject.CreatePropertySetsBase(cache, element, propertySetsCreated, "IfcPropertySetList", typeObject.HasPropertySets);
         }
      }

      internal static BuiltInParameter GetGUIDParameter(this IfcObjectDefinition objectDefinition)
      {
         if (objectDefinition is IfcTypeObject)
            return BuiltInParameter.IFC_TYPE_GUID;

         if (objectDefinition is IfcProject)
            return BuiltInParameter.IFC_PROJECT_GUID;
         if (objectDefinition is IfcSite)
            return BuiltInParameter.IFC_SITE_GUID;
         if (objectDefinition is IfcBuilding)
            return BuiltInParameter.IFC_BUILDING_GUID;

         return BuiltInParameter.IFC_GUID;
      }

      internal static void CreateParametersInternal(this IfcObjectDefinition objectDefinition, Document doc, Element element, HashSet<string> presentationLayerNames)
      {
         if (element != null)
         {
            // Set the element name.
            objectDefinition.SetName(doc, element);

            // Set the element description.
            objectDefinition.SetDescription(doc, element);

            // The list of materials.
            objectDefinition.SetMaterialParameter(doc, element);

            // Set the "IfcSystem" parameter.
            objectDefinition.SetSystemParameter(doc, element);

            // Set the element GUID.
            bool elementIsType = (element is ElementType);
            BuiltInParameter ifcGUIDId = objectDefinition.GetGUIDParameter();
            ExporterIFCUtils.AddValueString(element, new ElementId(ifcGUIDId), objectDefinition.GlobalId);

            // Set the "IfcExportAs" parameter.
            string ifcExportAs = IFCCategoryUtil.GetCustomCategoryName(objectDefinition);
            if (!string.IsNullOrWhiteSpace(ifcExportAs))
               IFCPropertySet.AddParameterString(doc, element, "IfcExportAs", ifcExportAs, objectDefinition.StepId);

            // Set the IFCElementAssembly Parameter
            IfcRelAggregates relAggregates = objectDefinition.Decomposes;
            if (relAggregates != null)
            {
               IfcElementAssembly elementAssembly = relAggregates.RelatingObject as IfcElementAssembly;
               if (elementAssembly != null)
               {
                  IFCPropertySet.AddParameterString(doc, element, "IfcElementAssembly", elementAssembly.Name, objectDefinition.StepId);
               }
            }

            // Set additional parameters (if any), e.g. for Classification assignments
            IDictionary<string, object> additionalParameters = objectDefinition.AdditionalIntParameters();
            foreach (KeyValuePair<string, object> parItem in additionalParameters)
            {
               if (parItem.Value is string)
                  IFCPropertySet.AddParameterString(doc, element, parItem.Key, (string)parItem.Value, objectDefinition.StepId);
               else if (parItem.Value is double)
                  IFCPropertySet.AddParameterDouble(doc, element, parItem.Key, UnitType.UT_Custom, (double)parItem.Value, objectDefinition.StepId);
               else if (parItem.Value is int)
                  IFCPropertySet.AddParameterInt(doc, element, parItem.Key, (int)parItem.Value, objectDefinition.StepId);
               else if (parItem.Value is bool)
                  IFCPropertySet.AddParameterBoolean(doc, element, parItem.Key, (bool)parItem.Value, objectDefinition.StepId);
            }

            IfcElementType elementType = objectDefinition as IfcElementType;
            if (elementType != null)
            {
               if (!string.IsNullOrWhiteSpace(elementType.ElementType))
                  IFCPropertySet.AddParameterString(doc, element, "IfcElementType", elementType.ElementType, elementType.StepId);
            }
            else
            {
               IfcObject ifcObject = objectDefinition as IfcObject;
               if (ifcObject != null)
               {
                  // Set "ObjectTypeOverride" parameter.
                  string objectTypeOverride = ifcObject.ObjectType;
                  if (!string.IsNullOrWhiteSpace(objectTypeOverride))
                     IFCPropertySet.AddParameterString(doc, element, "ObjectTypeOverride", objectTypeOverride, ifcObject.StepId);
                  IfcProduct product = ifcObject as IfcProduct;
                  if (product != null)
                     product.CreateParametersInternal(doc, element, presentationLayerNames);
               }
               else
               {
                  IfcDoorStyle doorStyle = objectDefinition as IfcDoorStyle;
                  if (doorStyle != null)
                     doorStyle.CreateParametersInternal(doc, element);
               }
            }
         }
      }

      public static IDictionary<string, object> AdditionalIntParameters(this IfcObjectDefinition objectDefinition)
      {
         Dictionary<string, object> additionalIntParameters = new Dictionary<string, object>();
         foreach(IfcRelAssociates associates in objectDefinition.HasAssociations)
         {
            IfcRelAssociatesClassification associatesClassification = associates as IfcRelAssociatesClassification;
            if (associatesClassification != null)
            {
               IfcClassification classification = null; ;
               string identification = string.Empty;
               string classifItemName = string.Empty;
               string paramValue = string.Empty;

               IfcClassificationSelect classificationSelect = associatesClassification.RelatingClassification;
               IfcClassificationReference classificationReference = classificationSelect as IfcClassificationReference;
               if (classificationReference != null)
               {
                  classification = classificationReference.ReferencedClassification();
                  classifItemName = classificationReference.Name;
                  identification = classificationReference.Identification;
               }
               else
                  classification = classificationSelect as IfcClassification;

               if (classification != null && !string.IsNullOrEmpty(classification.Name))
                  paramValue = "[" + classification.Name + "]";
               paramValue += identification;
               if (!string.IsNullOrEmpty(classifItemName))
                  paramValue += ":" + classifItemName;

               string paramName = string.Empty;
               for (int i = 0; i < 10; ++i)
               {
                  paramName = "ClassificationCode";
                  if (i > 0)
                     paramName = "ClassificationCode(" + i.ToString() + ")";
                  if (!additionalIntParameters.ContainsKey(paramName))
                     break;
               }
               if (!string.IsNullOrEmpty(paramName))
                  additionalIntParameters[paramName] = paramValue;
            }
         }
         return additionalIntParameters;
      }

      /// <summary>
      /// Creates or populates Revit element params based on the information contained in this class.
      /// </summary>
      /// <param name="doc">The document.</param>
      internal static void CreateParameters(this IfcObjectDefinition objectDefinition, CreateElementIfcCache cache, ElementId elementId, HashSet<string> presentationLayerNames)
      {
         Element element = cache.Document.GetElement(elementId);
         if (element == null)
            return;

         // Create Revit parameters corresponding to IFC entity values, not in a property set.
         objectDefinition.CreateParametersInternal(cache.Document, element, presentationLayerNames);

         // Now create parameters related to property sets.  Note we want to add the parameters above first,
         // so we can use them for creating schedules in CreatePropertySets.
         string propertySetsCreated = "";
         objectDefinition.CreatePropertySets(cache, element, propertySetsCreated);
      }

      /// <summary>
      /// Get the element ids created for this entity, for summary logging.
      /// </summary>
      /// <param name="createdElementIds">The creation list.</param>
      /// <remarks>May contain InvalidElementId; the caller is expected to remove it.</remarks>
      public static void GetCreatedElementIds(this IfcObjectDefinition objectDefinition, ISet<ElementId> createdElementIds, CreateElementIfcCache cache)
      {
         ElementId elementId;
         IfcGrid grid = objectDefinition as IfcGrid;
         if (grid != null)
         {
            foreach (IfcGridAxis axis in grid.UAxes)
            {
               if(cache.CreatedElements.TryGetValue(axis.StepId, out elementId) && elementId != ElementId.InvalidElementId)
                  createdElementIds.Add(elementId);
            }

            foreach (IfcGridAxis axis in grid.VAxes)
            {
               if (cache.CreatedElements.TryGetValue(axis.StepId, out elementId) && elementId != ElementId.InvalidElementId)
                  createdElementIds.Add(elementId);
            }

            foreach (IfcGridAxis axis in grid.WAxes)
            {
               if (cache.CreatedElements.TryGetValue(axis.StepId, out elementId) && elementId != ElementId.InvalidElementId)
                  createdElementIds.Add(elementId);
            }

         }
         else
         {
            if (cache.CreatedElements.TryGetValue(objectDefinition.StepId, out elementId))
            {
               // If we used ProjectInformation, don't report that.
               if (elementId != ElementId.InvalidElementId && elementId != Importer.TheCache.ProjectInformationId)
                  createdElementIds.Add(elementId);
            }
            IfcBuildingStorey buildingStorey = objectDefinition as IfcBuildingStorey;
            if (buildingStorey != null)
            {
               if (cache.CreatedViews.TryGetValue(objectDefinition.StepId, out elementId))
               {
                  if (elementId != ElementId.InvalidElementId)
                     createdElementIds.Add(elementId);
               }
            }
         }
      }

      /// <summary>
      /// Create one or more elements 
      /// </summary>
      /// <param name="doc">The document being populated.</param>
      /// <returns>The primary element associated with the IFCObjectDefinition, or InvalidElementId if it failed.</returns>
      public static ElementId CreateElement(this IfcObjectDefinition objectDefinition, CreateElementIfcCache cache, Transform globalTransform)
      {
         if (objectDefinition == null)
            return ElementId.InvalidElementId;
         // This would be a good place to check 'objDef.GlobalId'.

         ElementId elementId;
         cache.CreatedElements.TryGetValue(objectDefinition.StepId, out elementId);
         try
         {
            if ((elementId == ElementId.InvalidElementId || elementId == null) && !cache.InvalidForCreation.Contains(objectDefinition.StepId))
            {
               ElementId categoryId = objectDefinition.CategoryId(cache);

               IfcObject ifcObject = objectDefinition as IfcObject;
               if (ifcObject != null)
               {
                  IfcTypeObject relatingType = ifcObject.RelatingType();
                  if (relatingType != null)
                     relatingType.CreateElement(cache, Transform.Identity);
               }

               objectDefinition.CreateRoot(cache, globalTransform);
               objectDefinition.CreateSubElements(cache, globalTransform);
               if(cache.CreatedElements.TryGetValue(objectDefinition.StepId, out elementId) && elementId != ElementId.InvalidElementId)
                  objectDefinition.CreateParameters(cache, elementId, null);
               Importer.TheLog.AddCreatedEntity(cache, objectDefinition);
            }
         }
         catch (Exception ex)
         {
            if (objectDefinition != null)
            {
               cache.InvalidForCreation.Add(objectDefinition.StepId);
               Importer.TheLog.LogCreationError(objectDefinition, ex.Message, false);
            }
         }
         return elementId;
      }

      internal static void CreateRoot(this IfcObjectDefinition objectDefinition, CreateElementIfcCache cache, Transform globalTransform)
      {
         if (objectDefinition == null)
            return;

         foreach (IfcRelAssociatesMaterial relAssociatesMaterial in objectDefinition.HasAssociations.OfType<IfcRelAssociatesMaterial>())
            relAssociatesMaterial.RelatingMaterial.Create(cache);

         IfcProduct product = objectDefinition as IfcProduct;
         if (product != null)
         {
            product.CreateProduct(cache, true, globalTransform);
         }
         else
         {
            IfcZone zone = objectDefinition as IfcZone;
            if (zone != null)
               zone.CreateZone(cache, globalTransform);
         }
         return;
      }
      /// <summary>
      /// Create property sets for a given element.
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="element">The element being created.</param>
      /// <param name="propertySetsCreated">A concatenated string of property sets created, used to filter schedules.</returns>
      /// <param name="propertySetListName">The name of the parameter that contains the property set list name.</param>
      /// <param name="propertySets">The list of properties.</param>
      internal static void CreatePropertySetsBase(this IfcObjectDefinition objectDefinition, CreateElementIfcCache cache, Element element, string propertySetsCreated, string propertySetListName,
         IList<IfcPropertySetDefinition> propertySets)
      {
         if (propertySetsCreated == null)
            propertySetsCreated = "";

         if (propertySets != null && propertySets.Count > 0)
         {
            IFCParameterSetByGroup parameterGroupMap = IFCParameterSetByGroup.Create(element);
            foreach (IfcPropertySetDefinition propertySet in propertySets)
            {
               KeyValuePair<string, bool> newPropertySetCreated = propertySet.CreatePropertySet(cache, element, parameterGroupMap);
               if (!newPropertySetCreated.Value || string.IsNullOrWhiteSpace(newPropertySetCreated.Key))
                  continue;

               if (propertySetsCreated == "")
                  propertySetsCreated = newPropertySetCreated.Key;
               else
                  propertySetsCreated += ";" + newPropertySetCreated.Key;
            }
         }
         // Add property set-based parameters.
         // We are going to create this "fake" parameter so that we can filter elements in schedules based on their property sets.
         IFCPropertySet.AddParameterString(cache.Document, element, propertySetListName, propertySetsCreated, objectDefinition.StepId);
      }
   }
}