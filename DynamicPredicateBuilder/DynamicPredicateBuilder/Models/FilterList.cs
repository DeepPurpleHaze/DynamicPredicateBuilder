namespace GTIWebAPI.Models.Service.Filters
{
    using Context;
    using CsQuery.ExtensionMethods;
    using GTIWebAPI.Models.Security;
    using Repository;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;

    [Table("FilterList")]
    public partial class FilterList : IContainId, IEquatable<FilterList>
    {
        public int Id { get; set; }

        public int? RightControllerId { get; set; }

        public byte? Tail { get; set; }

        public int? PropertyDataTypeId { get; set; }

        [StringLength(1000)]
        public string PropertyName { get; set; }

        [StringLength(50)]
        public string PropertyDisplayName { get; set; }

        public int? DefaultSearchOptionId { get; set; }

        [StringLength(250)]
        public string DefaultValue { get; set; }

        [StringLength(250)]
        public string DefaultValueText { get; set; }

        public bool Required { get; set; }

        [StringLength(250)]
        public string ControllerName { get; set; }

        [StringLength(100)]
        public string Method { get; set; }

        public bool? NeedsOfficeIds { get; set; }

        public virtual PropertyDataType PropertyDataType { get; set; }

        public bool Equals(FilterList other)
        {
            bool result = true;
            if (this.Id == 0)
            {
                if (this.RightControllerId != other.RightControllerId
                    || this.Tail != other.Tail
                    || this.PropertyDataTypeId != other.PropertyDataTypeId
                    || this.PropertyName != other.PropertyName
                    || this.PropertyDisplayName != other.PropertyDisplayName
                    || this.DefaultSearchOptionId != other.DefaultSearchOptionId
                    || this.DefaultValue != other.DefaultValue
                    || this.DefaultValueText != other.DefaultValueText
                    || this.Required != other.Required
                    || this.ControllerName != other.ControllerName
                    || this.Method != other.Method
                    || this.NeedsOfficeIds != other.NeedsOfficeIds)
                {
                    result = false;
                }
            }
            else
            {
                result = this.Id == other.Id;
            }
            return result;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public FilterListDTO ToDTO()
        {
            return new FilterListDTO(this);
        }
    }

    public class FilterListDTO
    {
        public FilterListDTO() { }

        public FilterListDTO(FilterList item)
        {
            Id = item.Id;
            Tail = item.Tail;
            PropertyDataTypeId = item.PropertyDataTypeId;
            PropertyDataType = item.PropertyDataType?.TypeName;
            PropertyName = item.PropertyName;
            PropertyDisplayName = item.PropertyDisplayName;
            DefaultSearchOptionId = item.DefaultSearchOptionId;            
            DefaultValue = item.DefaultValue;
            DefaultValueText = item.DefaultValueText;
            FilterSet = item.DefaultValue == null ? false : true;
            List<SearchOption> temp = new List<SearchOption>();
            item.PropertyDataType?.SearchOptionPropertyDataTypeLink?.ForEach(d => temp.Add(d.SearchOption));
            SearchOptions = temp;
            DefaultSearchOptionName = item.DefaultSearchOptionId == null ? "" : SearchOptions.Where(d => d.Id == item.DefaultSearchOptionId).FirstOrDefault().Title;
            Required = item.Required;
            ControllerName = item.ControllerName;
            Method = item.Method;
            NeedsOfficeIds = item.NeedsOfficeIds;
        }

        public int Id { get; set; }
        
        public byte? Tail { get; set; }

        public int? PropertyDataTypeId { get; set; }

        public string PropertyDataType { get; set; }

        [StringLength(1000)]
        public string PropertyName { get; set; }

        [StringLength(50)]
        public string PropertyDisplayName { get; set; }

        public int? DefaultSearchOptionId { get; set; }

        public string DefaultSearchOptionName { get; set; }

        [StringLength(250)]
        public string DefaultValue { get; set; }

        [StringLength(250)]
        public string DefaultValueText { get; set; }

        public bool FilterSet { get; set; }

        public bool Required { get; set; }

        [StringLength(250)]
        public string ControllerName { get; set; }

        [StringLength(100)]
        public string Method { get; set; }

        public bool? NeedsOfficeIds { get; set; }

        public List<SearchOption> SearchOptions { get; set; }
    }

    public class FilterListRepository : ModelRepository<FilterList>
    {
        public FilterListRepository(IDbContextFactory factory)
            : base(ips, factory)
        { }

        public static string ips = ips ?? ConstructIps();

        public static string ConstructIps()
        {
            string res = @"PropertyDataType, PropertyDataType.SearchOptionPropertyDataTypeLink, PropertyDataType.SearchOptionPropertyDataTypeLink.SearchOption";
            return res;
        }

        public IEnumerable<FilterList> GetByControllerId(int controllerId)
        {
            return unitOfWork.Repository.Get(d => d.RightControllerId == controllerId, q => q.OrderBy(d => d.Tail), includeProperties: ips);
        }

        public IEnumerable<FilterList> GetByControllerName(string controllerName)
        {
            GenericUnitOfWork<RightController> uow = new GenericUnitOfWork<RightController>(factory);
            int controllerId = uow.Repository.Get(d => d.Name == controllerName).Select(d => d.Id).FirstOrDefault();
            uow.Dispose();
            return GetByControllerId(controllerId);
        }
    }
}
