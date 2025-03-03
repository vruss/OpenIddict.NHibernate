using System;
using System.ComponentModel;
using NHibernate.Mapping.ByCode;
using NHibernate.Mapping.ByCode.Conformist;
using OpenIddict.NHibernate.Models;

namespace OpenIddict.NHibernate.Mappings
{
    /// <summary>
    /// Defines a relational mapping for the Scope entity.
    /// </summary>
    /// <typeparam name="TScope">The type of the Scope entity.</typeparam>
    /// <typeparam name="TKey">The type of the Key entity.</typeparam>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class OpenIddictScopeMapping<TScope, TKey> : ClassMapping<TScope>
        where TScope : OpenIddictScope<TKey>
        where TKey : IEquatable<TKey>
    {
        public OpenIddictScopeMapping()
        {
            this.Id(scope => scope.Id, map =>
            {
                map.Generator(Generators.Identity);
            });

            this.Version(scope => scope.Version, map =>
            {
                map.Insert(true);
            });

            this.Property(scope => scope.Description, map =>
            {
                map.Length(10000);
            });

            this.Property(scope => scope.DisplayName);

            this.Property(scope => scope.Name, map =>
            {
                map.NotNullable(true);
                map.Unique(true);
            });

            this.Property(scope => scope.Properties, map =>
            {
                map.Length(10000);
            });

            this.Property(scope => scope.Resources, map =>
            {
                map.Length(10000);
            });

            this.Table("OpenIddictScopes");
        }
    }
}
