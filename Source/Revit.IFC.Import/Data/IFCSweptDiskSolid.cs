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
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.IFC;
using Revit.IFC.Common.Utility;
using Revit.IFC.Common.Enums;
using Revit.IFC.Import.Enums;
using Revit.IFC.Import.Geometry;
using Revit.IFC.Import.Utility;

using GeometryGym.Ifc;

namespace Revit.IFC.Import.Data
{
   public static class IFCSweptDiskSolid
   {
      private static IList<CurveLoop> CreateProfileCurveLoopsForDirectrix(this IfcSweptDiskSolid sweptDiskSolid, Curve directrix, out double startParam)
      {
         startParam = 0.0;

         if (directrix == null)
            return null;

         if (directrix.IsBound)
            startParam = directrix.GetEndParameter(0);

         Transform originTrf = directrix.ComputeDerivatives(startParam, false);

         if (originTrf == null)
            return null;

         // The X-dir of the transform of the start of the directrix will form the normal of the disk.
         Plane diskPlane = Plane.CreateByNormalAndOrigin(originTrf.BasisX, originTrf.Origin);

         IList<CurveLoop> profileCurveLoops = new List<CurveLoop>();

         CurveLoop diskOuterCurveLoop = new CurveLoop();
         double radius = IFCUnitUtil.ScaleLength(sweptDiskSolid.Radius);
         diskOuterCurveLoop.Append(Arc.Create(diskPlane, radius, 0, Math.PI));
         diskOuterCurveLoop.Append(Arc.Create(diskPlane, radius, Math.PI, 2.0 * Math.PI));
         profileCurveLoops.Add(diskOuterCurveLoop);

         if (!double.IsNaN(sweptDiskSolid.InnerRadius))
         {
            double innerRadius = IFCUnitUtil.ScaleLength(sweptDiskSolid.InnerRadius);
            if (Application.IsValidThickness(innerRadius))
            {
               CurveLoop diskInnerCurveLoop = new CurveLoop();
               diskInnerCurveLoop.Append(Arc.Create(diskPlane, innerRadius, 0, Math.PI));
               diskInnerCurveLoop.Append(Arc.Create(diskPlane, innerRadius, Math.PI, 2.0 * Math.PI));
               profileCurveLoops.Add(diskInnerCurveLoop);
            }
         }

         return profileCurveLoops;
      }

      private static IList<GeometryObject> SplitSweptDiskIntoValidPieces(this IfcSweptDiskSolid sweptDiskSolid, CurveLoop trimmedDirectrixInWCS, IList<CurveLoop> profileCurveLoops, SolidOptions solidOptions)
      {
         // If we have 0 or 1 curves, there is nothing we can do here.
         int numCurves = trimmedDirectrixInWCS.Count();
         if (numCurves < 2)
            return null;

         // We will attempt to represent the original description in as few pieces as possible.  
         IList<Curve> directrixCurves = new List<Curve>();
         foreach (Curve directrixCurve in trimmedDirectrixInWCS)
         {
            if (directrixCurve == null)
            {
               numCurves--;
               if (numCurves < 2)
                  return null;
               continue;
            }
            directrixCurves.Add(directrixCurve);
         }

         IList<GeometryObject> sweptDiskPieces = new List<GeometryObject>();

         // We will march along the directrix one curve at a time, trying to build a bigger piece of the sweep.  At the point that we throw an exception,
         // we will take the last biggest piece and start over.
         CurveLoop currentCurveLoop = new CurveLoop();
         Solid bestSolidSoFar = null;
         double pathAttachmentParam = directrixCurves[0].GetEndParameter(0);

         for (int ii = 0; ii < numCurves; ii++)
         {
            currentCurveLoop.Append(directrixCurves[ii]);
            try
            {
               Solid currentSolid = GeometryCreationUtilities.CreateSweptGeometry(currentCurveLoop, 0, pathAttachmentParam, profileCurveLoops,
                  solidOptions);
               bestSolidSoFar = currentSolid;
            }
            catch
            {
               if (bestSolidSoFar != null)
               {
                  sweptDiskPieces.Add(bestSolidSoFar);
                  bestSolidSoFar = null;
               }
            }

            // This should only happen as a result of the catch loop above.  We want to protect against the case where one or more pieces of the sweep 
            // are completely invalid.
            while (bestSolidSoFar == null && (ii < numCurves))
            {
               try
               {
                  currentCurveLoop = new CurveLoop();
                  currentCurveLoop.Append(directrixCurves[ii]);
                  profileCurveLoops = sweptDiskSolid.CreateProfileCurveLoopsForDirectrix(directrixCurves[ii], out pathAttachmentParam);

                  Solid currentSolid = GeometryCreationUtilities.CreateSweptGeometry(currentCurveLoop, 0, pathAttachmentParam, profileCurveLoops,
                     solidOptions);
                  bestSolidSoFar = currentSolid;
                  break;
               }
               catch
               {
                  ii++;
               }
            }
         }

         return sweptDiskPieces;
      }

      /// <summary>
      /// Return geometry for a particular representation item.
      /// </summary>
      /// <param name="shapeEditScope">The geometry creation scope.</param>
      /// <param name="unscaledLcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      /// <returns>Zero or more created geometries.</returns>
      internal static IList<GeometryObject> CreateGeometrySweptDiskSolid(this IfcSweptDiskSolid sweptDiskSolid, CreateElementIfcCache cache,
            IFCImportShapeEditScope shapeEditScope, Transform unscaledLcs, Transform scaledLcs, string guid)
      {
         Transform unscaledSweptDiskPosition = (unscaledLcs == null) ? Transform.Identity : unscaledLcs;
         Transform scaledSweptDiskPosition = (scaledLcs == null) ? Transform.Identity : scaledLcs;

         CurveLoop trimmedDirectrix = IFCGeometryUtil.TrimCurveLoop(sweptDiskSolid.StepId, sweptDiskSolid.Directrix, sweptDiskSolid.StartParam, sweptDiskSolid.EndParam);
         if (trimmedDirectrix == null)
            return null;

         CurveLoop trimmedDirectrixInWCS = IFCGeometryUtil.CreateTransformed(trimmedDirectrix, sweptDiskSolid.StepId, unscaledSweptDiskPosition, scaledSweptDiskPosition);

         // Create the disk.
         Curve firstCurve = null;
         foreach (Curve curve in trimmedDirectrixInWCS)
         {
            firstCurve = curve;
            break;
         }

         double startParam = 0.0;
         IList<CurveLoop> profileCurveLoops = sweptDiskSolid.CreateProfileCurveLoopsForDirectrix(firstCurve, out startParam);
         if (profileCurveLoops == null)
            return null;

         SolidOptions solidOptions = new SolidOptions(sweptDiskSolid.GetMaterialElementId(cache, shapeEditScope), shapeEditScope.GraphicsStyleId);
         IList<GeometryObject> myObjs = new List<GeometryObject>();

         try
         {
            Solid solid = GeometryCreationUtilities.CreateSweptGeometry(trimmedDirectrixInWCS, 0, startParam, profileCurveLoops, solidOptions);
            if (solid != null)
               myObjs.Add(solid);
         }
         catch (Exception ex)
         {
            // If we can't create a solid, we will attempt to split the Solid into valid pieces (that will likely have some overlap).
            if (ex.Message.Contains("self-intersections"))
            {
               Importer.TheLog.LogWarning(sweptDiskSolid.StepId, "The IfcSweptDiskSolid definition does not define a valid solid, likely due to self-intersections or other such problems; the profile probably extends too far toward the inner curvature of the sweep path. Creating the minimum number of solids possible to represent the geometry.", false);
               myObjs = sweptDiskSolid.SplitSweptDiskIntoValidPieces(trimmedDirectrixInWCS, profileCurveLoops, solidOptions);
            }
            else
               throw ex;
         }

         return myObjs;
      }
   }
}