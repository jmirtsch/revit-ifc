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
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   public static class IFCSite
   {
      public class ActiveSiteSetter : IDisposable
      {
         public ActiveSiteSetter(IfcSite ifcSite)
         {
            ActiveSite = ifcSite;
         }

         public static IfcSite ActiveSite
         {
            get;
            private set;
         }

         public void Dispose()
         {
            ActiveSite = null;
         }
      }

      private static double GetLatLongScale(int index)
      {
         switch (index)
         {
            case 0:
               return 1.0;
            case 1:
               return 60.0;
            case 2:
               return 3600.0;
            case 3:
               return 3600000000.0;
         }

         return 1.0;
      }

      

      /// <summary>
      /// Creates or populates Revit elements based on the information contained in this class.
      /// </summary>
      /// <param name="doc">The document.</param>
      internal static void SetHostSite(this IfcSite site, CreateElementIfcCache cache, Document doc, bool canRemoveSiteLocalPlacement)
      {
         ProjectLocation projectLocation = doc.ActiveProjectLocation;
         if (projectLocation != null)
         {
            SiteLocation siteLocation = projectLocation.GetSiteLocation();
            if (siteLocation != null)
            {
               // Some Tekla files may have invalid information here that would otherwise cause the
               // link to fail.  Recover with a warning.
               try
               {
                  IfcCompoundPlaneAngleMeasure refLatitude = site.RefLatitude, refLongitude = site.RefLongitude;
                  if(refLatitude != null)
                     siteLocation.Latitude = refLatitude.Angle() * Math.PI / 180.0;
                  if (refLongitude != null)
                     siteLocation.Longitude = refLongitude.Angle() * Math.PI / 180.0;
               }
               catch (Exception ex)
               {
                  Importer.TheLog.LogWarning(site.StepId, "Invalid latitude or longitude value supplied for IFCSITE: " + ex.Message, false);
               }
            }

            XYZ projectLoc = site.ObjectPlacement.TotalTransform().OfPoint(XYZ.Zero);
            if (!MathUtil.IsAlmostZero(projectLoc.Z))
               Importer.TheLog.LogError(site.StepId, "The Z-value of the IfcSite object placement relative transform should be 0.  This will be ignored in favor of the RefElevation value.", false);

            // Get true north from IFCProject.
            double trueNorth = 0.0;
            UV trueNorthUV = cache.TrueNorth;
            if (trueNorthUV != null)
            {
               double geometricAngle = Math.Atan2(trueNorthUV.V, trueNorthUV.U);
               // Convert from geometric angle to compass direction.
               // This involves two steps: (1) subtract PI/2 from the angle, staying in (-PI, PI], then (2) reversing the result.
               trueNorth = (geometricAngle > -Math.PI / 2.0) ? geometricAngle - Math.PI / 2.0 : geometricAngle + Math.PI * 1.5;
               trueNorth = -trueNorth;
            }

            double refElevation = site.RefElevation;
            if (double.IsNaN(refElevation))
               refElevation = 0;

            ProjectPosition projectPosition = new ProjectPosition(projectLoc.X, projectLoc.Y, refElevation, trueNorth);

            XYZ origin = canRemoveSiteLocalPlacement ? XYZ.Zero : new XYZ(projectLoc.X, projectLoc.Y, refElevation);
            projectLocation.SetProjectPosition(origin, projectPosition);

            // Now that we've set the project position, remove the site relative transform, if the file is created correctly (that is, all entities contained in the site
            // have the local placements relative to the site.
            if (canRemoveSiteLocalPlacement)
               IFCLocation.RemoveRelativeTransformForSite(site);
         }
      }

      /// <summary>
      /// Creates or populates Revit element params based on the information contained in this class.
      /// </summary>
      /// <param name="doc">The document.</param>
      /// <param name="element">The element.</param>
      internal static void CreateParametersInternal(this IfcSite site, Document doc, Element element)
      {
         string parameterName = "LandTitleNumber";

         // TODO: move this to new shared parameter names override function.
         if (element is ProjectInfo)
         {
            parameterName = "IfcSite " + parameterName;
         }

         if (element != null)
         {
            string landTitleNumber = site.LandTitleNumber;
            if (!string.IsNullOrWhiteSpace(landTitleNumber))
               IFCPropertySet.AddParameterString(doc, element, parameterName, landTitleNumber, site.StepId);
         }
      }
   }
}