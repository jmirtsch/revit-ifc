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
   public static class IFCCartesianTransformOperator
   {
      /// <summary>
      /// Calculate the X axis of a transform from the Z axis and an optional X direction, according to the IFC2x3 schema definition.
      /// </summary>
      /// <param name="zAxis">The required, normalized Z axis.</param>
      /// <param name="originalXAxis">The optional X axis from the IFC file, used as a guide.</param>
      /// <param name="id">The id of the IfcCartesianTransformOperator entity, used for error reporting.</param>
      /// <returns>A normalized vector orthogonal to the input zAxis.</returns>
      private static XYZ CalculateXAxisFromZAxis(XYZ zAxis, XYZ originalXAxis, int id)
      {
         // Assumes that zAxis exists and is normalized.
         if (zAxis == null)
            return null;

         XYZ xAxis = null;
         if (originalXAxis == null)
         {
            // The IFC calculation checks that zAxis is not (1,0,0).  We also check (-1,0,0).
            if (!MathUtil.IsAlmostEqual(Math.Abs(zAxis.X), 1.0))
            {
               xAxis = XYZ.BasisX;
            }
            else
            {
               if (MathUtil.IsAlmostEqual(zAxis.X, -1.0))
                  Importer.TheLog.LogWarning(id, "The IFC schema definition would generate an incorrect X basis vector in this case.  Correcting.", false);
               xAxis = XYZ.BasisY;
            }
         }
         else
         {
            if (MathUtil.VectorsAreParallel(originalXAxis, zAxis))
            {
               // This may be because X and Z are both set to the same vector, or Z was unset and set to +Z, which is the same as originalXAxis.  We'll correct for this in the caller.
               return null;
            }

            xAxis = originalXAxis.Normalize();
         }

         xAxis = (xAxis - (xAxis.DotProduct(zAxis) * zAxis)).Normalize();
         return xAxis;
      }

      /// <summary>
      /// Calculate a transform from zero or more axis vectors, according to the IFC2x3 schema definition.
      /// </summary>
      /// <param name="axis1">The X axis, or null.</param>
      /// <param name="axis2">The Y axis, or null.</param>
      /// <param name="axis3">The Z axis, or null.</param>
      /// <param name="orig">The origin.</param>
      /// <param name="dim">The dimensionality of the arguments (either 2 or 3).  If dim is 2, then axis3 will be ignored, as will
      /// the Z component of axis1 and axis2.</param>
      /// <param name="id">The id of the IfcCartesianTransformOperator entity, used for error reporting.</param>
      /// <returns>The transform.</returns>
      /// <remarks>This is an adaption of the IfcBaseAxis function in the IFC2x3_TC1.exp file that defines
      /// how the basis vectors should be calculated for IfcCartesianTransformOperator.</remarks>
      private static Transform CreateTransformUsingIfcBaseAxisCalculation(XYZ axis1, XYZ axis2, XYZ axis3, XYZ orig, int dim, int id)
      {
         XYZ xAxis = null;
         XYZ yAxis = null;
         XYZ zAxis = null;

         if (dim == 3)
         {
            // Only do the calculations below if any of the vectors are missing, or the 3 vectors aren't orthonormal.
            // The input vectors should already be normalized.
            if (axis1 == null || axis2 == null || axis3 == null ||
               !MathUtil.VectorsAreOrthogonal(axis1, axis2) ||
               !MathUtil.VectorsAreOrthogonal(axis1, axis3) ||
               !MathUtil.VectorsAreOrthogonal(axis2, axis3))
            {
               // Note that the IFC schema definition does not take into account the case where the zAxis isn't defined, and the xAxis is +/-Z.
               zAxis = (axis3 == null) ? XYZ.BasisZ : axis3.Normalize();
               xAxis = CalculateXAxisFromZAxis(zAxis, axis1, id);
               if (xAxis == null)
               {
                  // This may be because axis1 and axis3 are both set to the same vector, or axis3 was unset and axis1 was set to +/-Z.  Correct this below.
                  Importer.TheLog.LogWarning(id, "The IFC schema definition would generate an incorrect X basis vector in this case.  Correcting.", false);
                  if (axis3 == null)
                  {
                     if (axis1 == null)
                        Importer.TheLog.LogError(id, "Invalid basis vectors.  Can't correct, aborting.", true);
                     xAxis = axis1;
                     zAxis = XYZ.BasisY;
                  }
                  else
                     xAxis = XYZ.BasisX;
               }

               yAxis = zAxis.CrossProduct(xAxis);

               // Note that according to the IFC schema, the axis2 argument is effectively ignored.  We'll check it for consistency.
               if (axis2 != null)
               {
                  int vecsAreParallel = MathUtil.VectorsAreParallel2(yAxis, axis2);
                  if (vecsAreParallel != 1)
                  {
                     // In the specific case where the vectors are anti-parallel, we'll create a mirrored transform.
                     if (vecsAreParallel == -1)
                        yAxis = axis2;
                     else
                        Importer.TheLog.LogWarning(id, "Inconsistent basis vectors based on the IFC schema definition may cause a difference in orientation for related objects.", false);
                  }
               }
            }
            else
            {
               xAxis = axis1;
               yAxis = axis2;
               zAxis = axis3;
            }
         }
         else if (dim == 2)
         {
            if (axis1 != null && !MathUtil.IsAlmostZero(xAxis.Z))
               Importer.TheLog.LogWarning(id, "Invalid X basis vector, ignoring.", true);

            if (axis2 != null && !MathUtil.IsAlmostZero(yAxis.Z))
               Importer.TheLog.LogWarning(id, "Invalid Y basis vector, ignoring.", true);

            zAxis = XYZ.BasisZ;

            if (axis1 != null)
            {
               xAxis = axis1.Normalize();
               yAxis = new XYZ(-xAxis.Y, xAxis.X, 0.0);

               if (axis2 != null)
               {
                  double dot = axis2.DotProduct(yAxis);
                  if (dot < 0.0)
                     yAxis = -yAxis;
               }
            }
            else if (axis2 != null)
            {
               yAxis = axis2.Normalize();
               xAxis = new XYZ(yAxis.Y, -yAxis.X, 0.0);
            }
            else
            {
               xAxis = XYZ.BasisX;
               yAxis = XYZ.BasisY;
            }
         }
         else
            Importer.TheLog.LogError(id, "Can't handle dimensionality of " + dim + " in calculating basis vector.", true);

         Transform transform = Transform.CreateTranslation(orig);
         transform.BasisX = xAxis;
         transform.BasisY = yAxis;
         transform.BasisZ = zAxis;
         return transform;
      }

      internal static Transform GetTransform(this IfcCartesianTransformationOperator cartesianTransformationOperator)
      {
         IfcCartesianPoint localOrigin = cartesianTransformationOperator.LocalOrigin;
         XYZ origin = null;
         if (localOrigin != null)
            origin = IFCPoint.ProcessScaledLengthIFCCartesianPoint(localOrigin);
         else
            origin = XYZ.Zero;

         IfcDirection axis1 = cartesianTransformationOperator.Axis1;
         XYZ xAxis = null;
         if (axis1 != null)
            xAxis = IFCPoint.ProcessNormalizedIFCDirection(axis1);

         IfcDirection axis2 = cartesianTransformationOperator.Axis2;
         XYZ yAxis = null;
         if (axis2 != null)
            yAxis = IFCPoint.ProcessNormalizedIFCDirection(axis2);

         double scale = cartesianTransformationOperator.Scale, scaleY = 1, scaleZ = 1;

         XYZ zAxis = null;

         // Assume that the dimensionality of the IfcCartesianTransformationOperator is 2, unless determined otherwise below.
         int dim = 2;

         IfcCartesianTransformationOperator2DnonUniform cartesianTransformationOperator2DnonUniform = cartesianTransformationOperator as IfcCartesianTransformationOperator2DnonUniform;
         if(cartesianTransformationOperator2DnonUniform != null)
            scaleY = cartesianTransformationOperator2DnonUniform.Scale2;
         else
         {
            IfcCartesianTransformationOperator3D cartesianTransformationOperator3D = cartesianTransformationOperator as IfcCartesianTransformationOperator3D;
            if (cartesianTransformationOperator3D != null)
            {
               dim = 3;
               IfcDirection axis3 = cartesianTransformationOperator3D.Axis3;
               if (axis3 != null)
                  zAxis = IFCPoint.ProcessNormalizedIFCDirection(axis3);
               IfcCartesianTransformationOperator3DnonUniform cartesianTransformationOperator3DnonUniform = cartesianTransformationOperator3D as IfcCartesianTransformationOperator3DnonUniform;
               if (cartesianTransformationOperator3DnonUniform != null)
               {
                  scaleY = cartesianTransformationOperator3DnonUniform.Scale2;
                  scaleZ = cartesianTransformationOperator3DnonUniform.Scale3;
               }
            }
         }

         // Set the axes based on what is specified.
         return CreateTransformUsingIfcBaseAxisCalculation(xAxis, yAxis, zAxis, origin, dim, cartesianTransformationOperator.StepId);
      }
   }
}