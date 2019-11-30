namespace GTIWebAPI.Models.Service.Filters
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("SearchOptionPropertyDataTypeLink")]
    public partial class SearchOptionPropertyDataTypeLink
    {
        public int Id { get; set; }

        public int? SearchOptionId { get; set; }

        public int? PropertyDataTypeId { get; set; }

       // public virtual PropertyDataType PropertyDataType { get; set; }

        public virtual SearchOption SearchOption { get; set; }
    }
}
