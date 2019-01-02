using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
   public static class IFCPolygonalFaceSet 
   {
      internal static void CreateShapePolygonalFaceSet(this IfcPolygonalFaceSet polygonalFaceSet, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         using (BuilderScope bs = shapeEditScope.InitializeBuilder(IFCShapeBuilderType.TessellatedShapeBuilder))
         {
            TessellatedShapeBuilderScope tsBuilderScope = bs as TessellatedShapeBuilderScope;

            tsBuilderScope.StartCollectingFaceSet();
            IList<int> pnIndex = polygonalFaceSet.PnIndex;
            IList<XYZ> coordinates = polygonalFaceSet.Coordinates.GetPoints();

            // Create the face set from IFCIndexedPolygonalFace
            foreach (IfcIndexedPolygonalFace face in polygonalFaceSet.Faces)
            {
               tsBuilderScope.StartCollectingFace(polygonalFaceSet.GetMaterialElementId(cache, shapeEditScope));

               IList<XYZ> loopVertices = new List<XYZ>();
               foreach (int vertInd in face.CoordIndex)
               {
                  int actualVIdx = vertInd - 1;       // IFC starts the list position at 1
                  if (pnIndex != null)
                     actualVIdx = pnIndex[actualVIdx] - 1;
                  XYZ vertex = coordinates[actualVIdx];
                  loopVertices.Add(scaledLcs.OfPoint(vertex));
               }
               IList<XYZ> validVertices;
               IFCGeometryUtil.CheckAnyDistanceVerticesWithinTolerance(polygonalFaceSet.StepId, shapeEditScope, loopVertices, out validVertices);

               bool bPotentiallyAbortFace = false;
               if (!tsBuilderScope.AddLoopVertices(polygonalFaceSet.StepId, validVertices))
                  bPotentiallyAbortFace = true;

               IfcIndexedPolygonalFaceWithVoids indexedPolygonalFaceWithVoids = face as IfcIndexedPolygonalFaceWithVoids;
               if(indexedPolygonalFaceWithVoids != null)
               {
                  // Handle holes
                  foreach (IList<int> innerLoop in indexedPolygonalFaceWithVoids.InnerCoordIndices)
                  {
                     IList<XYZ> innerLoopVertices = new List<XYZ>();
                     foreach (int innerVerIdx in innerLoop)
                     {
                        int actualVIdx = innerVerIdx - 1;
                        if (pnIndex != null)
                           actualVIdx = pnIndex[actualVIdx] - 1;
                        XYZ vertex = coordinates[actualVIdx];
                        // add vertex to the loop
                        innerLoopVertices.Add(scaledLcs.OfPoint(vertex));
                     }
                     IList<XYZ> validInnerV;
                     IFCGeometryUtil.CheckAnyDistanceVerticesWithinTolerance(polygonalFaceSet.StepId, shapeEditScope, innerLoopVertices, out validInnerV);

                     if (!tsBuilderScope.AddLoopVertices(polygonalFaceSet.StepId, validInnerV))
                        bPotentiallyAbortFace = true;
                  }
               }

               if (bPotentiallyAbortFace)
                  tsBuilderScope.AbortCurrentFace();
               else
                  tsBuilderScope.StopCollectingFace();
            }

            IList<GeometryObject> createdGeometries = tsBuilderScope.CreateGeometry(guid);
            if (createdGeometries != null)
            {
               foreach (GeometryObject createdGeometry in createdGeometries)
               {
                  shapeEditScope.Solids.Add(IFCSolidInfo.Create(polygonalFaceSet.StepId, createdGeometry));
               }
            }
         }
      }

   }
}