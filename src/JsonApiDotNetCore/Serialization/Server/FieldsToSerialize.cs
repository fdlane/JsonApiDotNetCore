using JsonApiDotNetCore.Internal.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Models.Annotation;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Services.Contract;

namespace JsonApiDotNetCore.Serialization.Server
{
    /// <inheritdoc/>
    public class FieldsToSerialize : IFieldsToSerialize
    {
        private readonly IResourceGraph _resourceGraph;
        private readonly IEnumerable<IQueryConstraintProvider> _constraintProviders;
        private readonly IResourceDefinitionProvider _resourceDefinitionProvider;

        public FieldsToSerialize(
            IResourceGraph resourceGraph,
            IEnumerable<IQueryConstraintProvider> constraintProviders,
            IResourceDefinitionProvider resourceDefinitionProvider)
        {
            _resourceGraph = resourceGraph;
            _constraintProviders = constraintProviders;
            _resourceDefinitionProvider = resourceDefinitionProvider;
        }

        /// <inheritdoc/>
        public IReadOnlyCollection<AttrAttribute> GetAttributes(Type resourceType, RelationshipAttribute relationship = null)
        {   
            var sparseFieldSetAttributes = _constraintProviders
                .SelectMany(p => p.GetConstraints())
                .Where(expressionInScope => relationship == null
                    ? expressionInScope.Scope == null
                    : expressionInScope.Scope != null && expressionInScope.Scope.Fields.Last().Equals(relationship))
                .Select(expressionInScope => expressionInScope.Expression)
                .OfType<SparseFieldSetExpression>()
                .SelectMany(sparseFieldSet => sparseFieldSet.Attributes)
                .ToHashSet();

            if (!sparseFieldSetAttributes.Any())
            {
                sparseFieldSetAttributes = GetViewableAttributes(resourceType);
            }

            var resourceDefinition = _resourceDefinitionProvider.Get(resourceType);
            if (resourceDefinition != null)
            {
                var inputExpression = sparseFieldSetAttributes.Any() ? new SparseFieldSetExpression(sparseFieldSetAttributes) : null;
                var outputExpression = resourceDefinition.OnApplySparseFieldSet(inputExpression);

                if (outputExpression == null)
                {
                    sparseFieldSetAttributes = GetViewableAttributes(resourceType);
                }
                else
                {
                    sparseFieldSetAttributes.IntersectWith(outputExpression.Attributes);
                }
            }

            return sparseFieldSetAttributes;
        }

        private HashSet<AttrAttribute> GetViewableAttributes(Type resourceType)
        {
            return _resourceGraph.GetAttributes(resourceType)
                .Where(attr => attr.Capabilities.HasFlag(AttrCapabilities.AllowView))
                .ToHashSet();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Note: this method does NOT check if a relationship is included to determine
        /// if it should be serialized. This is because completely hiding a relationship
        /// is not the same as not including. In the case of the latter,
        /// we may still want to add the relationship to expose the navigation link to the client.
        /// </remarks>
        public IReadOnlyCollection<RelationshipAttribute> GetRelationships(Type type)
        {
            return _resourceGraph.GetRelationships(type);
        }
    }
}
