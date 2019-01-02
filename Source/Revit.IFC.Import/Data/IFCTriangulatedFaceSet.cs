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
   public static class IFCTriangulatedFaceSet 
   {
      internal static void CreateShapeTriangulatedFaceSet(this IfcTriangulatedFaceSet triangulatedFaceSet, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         using (BuilderScope bs = shapeEditScope.InitializeBuilder(IFCShapeBuilderType.TessellatedShapeBuilder))
         {
            TessellatedShapeBuilderScope tsBuilderScope = bs as TessellatedShapeBuilderScope;

            tsBuilderScope.StartCollectingFaceSet();

            List<XYZ> points = triangulatedFaceSet.Coordinates.GetPoints().Select(x=> scaledLcs.OfPoint(x)).ToList();
            IList<int> pnIndex = triangulatedFaceSet.PnIndex;
            // Create triangle face set from CoordIndex. We do not support the Normals yet at this point
            foreach (Tuple<int,int,int> triIndex in triangulatedFaceSet.CoordIndex)
            {
               // This is a defensive check in an unlikely situation that the index is larger than the data
               if (triIndex.Item1 > points.Count || triIndex.Item2 > points.Count || triIndex.Item3 > points.Count)
               {
                  continue;
               }

               tsBuilderScope.StartCollectingFace(triangulatedFaceSet.GetMaterialElementId(cache, shapeEditScope));

               IList<XYZ> loopVertices = new List<XYZ>();

               int actualVIdx = triIndex.Item1 - 1;
               if (pnIndex != null)
                  actualVIdx = pnIndex[actualVIdx] - 1;
               loopVertices.Add(points[actualVIdx]);
               actualVIdx = triIndex.Item2 - 1;
               if (pnIndex != null)
                  actualVIdx = pnIndex[actualVIdx] - 1;
               loopVertices.Add(points[actualVIdx]);
               actualVIdx = triIndex.Item3 - 1;
               if (pnIndex != null)
                  actualVIdx = pnIndex[actualVIdx] - 1;
               loopVertices.Add(points[actualVIdx]);

               // Check triangle that is too narrow (2 vertices are within the tolerance
               IList<XYZ> validVertices;
               IFCGeometryUtil.CheckAnyDistanceVerticesWithinTolerance(triangulatedFaceSet.StepId, shapeEditScope, loopVertices, out validVertices);

               // We are going to catch any exceptions if the loop is invalid.  
               // We are going to hope that we can heal the parent object in the TessellatedShapeBuilder.
               bool bPotentiallyAbortFace = false;

               int count = validVertices.Count;
               if (validVertices.Count < 3)
               {
                  Importer.TheLog.LogComment(triangulatedFaceSet.StepId, "Too few distinct loop vertices (" + count + "), ignoring.", false);
                  bPotentiallyAbortFace = true;
               }
               else
               {
                  if (!tsBuilderScope.AddLoopVertices(triangulatedFaceSet.StepId, validVertices))
                     bPotentiallyAbortFace = true;
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
                  shapeEditScope.Solids.Add(IFCSolidInfo.Create(triangulatedFaceSet.StepId, createdGeometry));
               }
            }
         }
      }
   }
}