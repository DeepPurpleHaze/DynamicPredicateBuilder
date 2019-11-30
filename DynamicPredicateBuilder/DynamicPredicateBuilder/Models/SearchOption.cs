namespace GTIWebAPI.Models.Service.Filters
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    [Table("SearchOption")]
    public partial class SearchOption
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public SearchOption() { }

        public int Id { get; set; }

        [StringLength(25)]
        public string Title { get; set; }        
    }

    public enum SearchOptions
    {
        Equal = 1,
        NotEqual = 2,
        GreaterThan = 3,
        GreaterThanOrEqual = 4,
        LessThan = 5,
        LessThanOrEqual = 6,
        GreaterThan_LessThan = 7,
        GreaterThanOrEqual_LessThanOrEqual = 8,
        GreaterThanOrEqual_LessThan = 9,
        GreaterThan_LessThanOrEqual = 10,
        Contains = 11,
        NotContains = 12,
        True = 13,
        False = 14,
        Container = 15,
        Route = 16,
        RoutePoint = 17,
        IsNull = 18,
        NotNull = 19,
        CollectionFilled = 20
    }
}
