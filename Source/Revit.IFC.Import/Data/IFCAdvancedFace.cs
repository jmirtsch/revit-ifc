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
   /// <summary>
   /// Class that represents IfcAdvancedFace entity
   /// </summary>
   public static class IFCAdvancedFace 
   {
      /// <summary>
      /// Create geometry for a particular representation item.
      /// </summary>
      /// <param name="shapeEditScope">The geometry creation scope.</param>
      /// <param name="lcs">Local coordinate system for the geometry, without scale.</param>
      /// <param name="scaledLcs">Local coordinate system for the geometry, including scale, potentially non-uniform.</param>
      /// <param name="guid">The guid of an element for which represntation is being created.</param>
      internal static bool CreateShapeAdvancedFace(this IfcAdvancedFace advancedFace, CreateElementIfcCache cache, IFCImportShapeEditScope shapeEditScope, Transform lcs, Transform scaledLcs, string guid)
      {
         if (shapeEditScope.BuilderType != IFCShapeBuilderType.BrepBuilder)
         {
            throw new InvalidOperationException("AdvancedFace can only be created using BrepBuilder");
         }

         // We may revisit this face on a second pass, after a first attempt to create a Solid failed.  Ignore this face.
         if (cache.InvalidForCreation.Contains(advancedFace.StepId))
            return false;

         BrepBuilderScope brepBuilderScope = shapeEditScope.BuilderScope as BrepBuilderScope;

         Transform localTransform = lcs != null ? lcs : Transform.Identity;

         bool isValidForCreation = true;
         brepBuilderScope.StartCollectingFace(advancedFace.FaceSurface, localTransform, advancedFace.SameSense, advancedFace.GetMaterialElementId(cache, shapeEditScope));

         foreach (IfcFaceBound faceBound in advancedFace.Bounds)
         {
            try
            {
               brepBuilderScope.InitializeNewLoop();

               faceBound.CreateShapeFaceBound(cache, shapeEditScope, lcs, scaledLcs, guid);
               isValidForCreation = !cache.InvalidForCreation.Contains(faceBound.StepId) || (!brepBuilderScope.HaveActiveFace());

               brepBuilderScope.StopConstructingLoop(isValidForCreation);

               if (!isValidForCreation)
                  break;
            }
            catch
            {
               cache.InvalidForCreation.Add(advancedFace.StepId);
               break;
            }
         }

         brepBuilderScope.StopCollectingFace(isValidForCreation);
         return true;
      }
   }
}