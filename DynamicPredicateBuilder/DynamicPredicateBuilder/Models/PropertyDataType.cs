namespace GTIWebAPI.Models.Service.Filters
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    [Table("PropertyDataType")]
    public partial class PropertyDataType
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public PropertyDataType()
        {
            SearchOptionPropertyDataTypeLink = new HashSet<SearchOptionPropertyDataTypeLink>();
        }

        public int Id { get; set; }

        [StringLength(50)]
        public string TypeName { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<SearchOptionPropertyDataTypeLink> SearchOptionPropertyDataTypeLink { get; set; }
    }

    public enum PropertyDataTypes
    {
        Integer = 1,
        Decimal = 2,
        Link = 3,
        Date = 4,
        String = 5,
        Bool = 7,
        Container = 8,
        Route = 9,
        Collection = 11
    }
}
