using GTIWebAPI.Models.Service.Filters;
using System;

namespace GTIWebAPI.Models.Service
{
    public class SearchObject : ICloneable
    {
        public SearchObject() { }

        public SearchObject(string property, dynamic values, SearchOptions searchOptionId, PropertyDataTypes propertyDataType)
        {
            Property = property;
            Values = values;
            SearchOptionId = (int)searchOptionId;
            PropertyDataTypeId = (int)propertyDataType;
        }

        //public SearchObject(FilterListDTO filter)
        //{
        //    Property = filter.PropertyName;


        //    Values = filter.DefaultValue;


        //    SearchOptionId = filter.DefaultSearchOptionId.GetValueOrDefault();
        //    PropertyDataTypeId = filter.PropertyDataTypeId.GetValueOrDefault();
        //}

        public string Property { get; set; }

        public dynamic Values { get; set; }

        public int SearchOptionId { get; set; }

        public int PropertyDataTypeId { get; set; }

        public object Clone()
        {
            return new SearchObject(this.Property, this.Values, (SearchOptions)this.SearchOptionId, (PropertyDataTypes)this.PropertyDataTypeId);
        }
    }
}