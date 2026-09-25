using System;
using System.Linq.Expressions;
using Eshava.Storm.Extensions;
using Eshava.Storm.MetaData.Enums;
using Eshava.Storm.MetaData.Extensions;
using Eshava.Storm.MetaData.Models;

namespace Eshava.Storm.MetaData.Builders
{
	public sealed class EntityTypeBuilder<TEntity> where TEntity : class
	{
		private readonly Entity _entity;

		internal EntityTypeBuilder()
		{
			var type = typeof(TEntity);

			// A new entity is published by the type analyzer once the configuration is applied and the analysis is complete
			_entity = EntityCache.GetEntity(type) ?? new Entity(type, ConfigurationSource.Explicit);
		}

		internal Entity Entity => _entity;

		public EntityTypeBuilder<TEntity> ToTable(string tableName, string schema = null)
		{
			if (schema.IsNullOrEmpty())
			{
				_entity.SetTableName($"[{tableName}]");
			}
			else
			{
				_entity.SetTableName($"[{schema}].[{tableName}]");
			}

			return this;
		}

		public void HasKey(Expression<Func<TEntity, object>> keyExpression)
		{
			var members = keyExpression.GetMemberAccessList();

			foreach (var member in members)
			{
				// Create property if not exists
				new PropertyBuilder(_entity, member, ConfigurationSource.Explicit).IsPrimaryKey();
			}
		}

		public EntityTypeBuilder<TEntity> OwnsOne<TRelatedEntity>(Expression<Func<TEntity, TRelatedEntity>> navigationExpression, Action<ReferenceOwnershipBuilder<TEntity, TRelatedEntity>> buildAction) where TRelatedEntity : class
		{
			var ownsOnePropertyName = navigationExpression.GetMemberAccess().GetSimpleMemberName();
			// Create property if not exists
			Property(navigationExpression);
			var ownsOneProperty = _entity.GetProperty(ownsOnePropertyName);

			var builder = new ReferenceOwnershipBuilder<TEntity, TRelatedEntity>(ownsOneProperty);
			buildAction(builder);

			return this;
		}

		public PropertyBuilder<TProperty> Property<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
		{
			return new PropertyBuilder<TProperty>(_entity, propertyExpression.GetMemberAccess(), ConfigurationSource.Explicit);
		}

		public EntityTypeBuilder<TEntity> Ignore<TProperty>(Expression<Func<TEntity, TProperty>> propertyExpression)
		{
			var propertyBuilder = new PropertyBuilder<TProperty>(_entity, propertyExpression.GetMemberAccess(), ConfigurationSource.Explicit);
			propertyBuilder.Ignore();

			return this;
		}
	}
}