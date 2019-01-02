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
using Revit.IFC.Import.Data;
using Revit.IFC.Common.Utility;
using Revit.IFC.Common.Enums;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Utility
{
   /// <summary>
   /// Utilities for IFCElement
   /// </summary>
   public static class IFCElementUtil
   {
      /// <summary>
      /// Gets the host of a hosted element, if any.
      /// </summary>
      /// <param name="hostedElement">The hosted element.</param>
      /// <returns>The host, or null.</returns>
      static public IfcElement GetHost(this IfcElement hostedElement)
      {
         if (hostedElement == null)
            return null;

         IfcRelFillsElement fillsElement = hostedElement.FillsVoids.FirstOrDefault();
         if (fillsElement == null)
            return null;
         IfcOpeningElement openingElement = fillsElement.RelatingOpeningElement;
         if (openingElement == null)
            return null;
         IfcRelVoidsElement voidsElement = openingElement.VoidsElement;
         if (voidsElement == null)
            return null;
         return voidsElement.RelatingBuildingElement;
      }

      /// <summary>
      /// Gets the elements hosted by this host element, if any.
      /// </summary>
      /// <param name="hostElement">The host element.</param>
      /// <returns>The hosted elements, or null.  An unfilled opening counts as a hosted element.</returns>
      public static IList<IfcElement> GetHostedElements(this IfcElement hostElement)
      {
         if (hostElement == null)
            return null;

         IList<IfcFeatureElementSubtraction> openings = hostElement.HasOpenings.Select(x=>x.RelatedOpeningElement).ToList();
         if (openings == null || (openings.Count == 0))
            return null;

         List<IfcElement> hostedElements = new List<IfcElement>();
         foreach (IfcFeatureElementSubtraction opening in openings)
         {
            IfcOpeningElement openingElement = opening as IfcOpeningElement;
            if (opening == null)
               hostedElements.Add(opening);
            else
            {
               IList<IfcRelFillsElement> fillings = openingElement.HasFillings;
               if(fillings.Count == 0)
                  hostedElements.Add(openingElement);
               else
                  hostedElements.AddRange(fillings.Select(x=>x.RelatedBuildingElement));
            }
         }

         return hostedElements;
      }

      /// <summary>
      /// Returns a category id valid for DirectShape or DirestShapeType creation.
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="categoryId">The original category id.</param>
      /// <param name="id">The id of the creator, for warning use.</param>
      /// <returns>The original category id, or BuiltInCategory.OST_GenericModel, if it can't be used.</returns>
      static public ElementId GetDSValidCategoryId(Document doc, ElementId categoryId, int id)
      {
         if (!DirectShape.IsValidCategoryId(categoryId, doc))
         {
            Importer.TheLog.LogWarning(id, "Creating DirectShape or DirectShapeType with disallowed category id: " + categoryId + ", reverting to Generic Models.", true);
            return new ElementId(BuiltInCategory.OST_GenericModel);
         }

         return categoryId;
      }

      static public DirectShape CreateElement(CreateElementIfcCache cache, ElementId categoryId, string dataGUID, IList<GeometryObject> geomObjs, int id)
      {
         DirectShape directShape = CreateElement(cache.Document, categoryId, dataGUID, geomObjs, id);
         if(directShape != null)
            cache.CreatedElements[id] = directShape.Id;
         return directShape;

      }
      /// <summary>
      /// Create a DirectShape, and set its options accordingly.
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="categoryId">The category of the DirectShape.</param>
      /// <param name="dataGUID">The GUID of the data creating the DirectShape.</param>
      /// <param name="geomObjs">The list of geometries to add to the DirectShape.</param>
      /// <param name="id">The id of the IFCEntity object creating the DirectShape.</param>
      /// <returns>The DirectShape.</returns>
      static public DirectShape CreateElement(Document doc, ElementId categoryId, string dataGUID, IList<GeometryObject> geomObjs, int id)
      {
         string appGUID = Importer.ImportAppGUID();
         DirectShape directShape = DirectShape.CreateElement(doc, GetDSValidCategoryId(doc, categoryId, id));
         if (directShape == null)
            return null;
         directShape.ApplicationId = appGUID;
         directShape.ApplicationDataId = dataGUID;

         // Note: we use the standard options for the DirectShape that is created.  This includes but is not limited to:
         // Referenceable: true.
         // Room Bounding: if applicable, user settable.

         if (geomObjs != null)
            directShape.SetShape(geomObjs);

         return directShape;
      }

      /// <summary>
      /// Create a DirectShapeType.
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="name">The name of the DirectShapeType.</param>
      /// <param name="categoryId">The category of the DirectShape.</param>
      /// <param name="id">The id of the IFCEntity object creating the DirectShape.</param>
      /// <returns>The DirectShape.</returns>
      static public DirectShapeType CreateElementType(Document doc, string name, ElementId categoryId, int id)
      {
         DirectShapeType directShapeType = DirectShapeType.Create(doc, name, IFCElementUtil.GetDSValidCategoryId(doc, categoryId, id));
         Importer.TheCache.CreatedDirectShapeTypes[id] = directShapeType.Id;
         return directShapeType;
      }
   }
}