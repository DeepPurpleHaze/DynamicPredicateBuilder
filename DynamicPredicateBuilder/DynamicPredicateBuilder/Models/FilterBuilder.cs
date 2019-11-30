using LinqKit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace GTIWebAPI.Models.Service.Filters
{
    public static class FilterBuilder
    {
        public static void BuildPredicate<T>(ref Expression<Func<T, bool>> predicate, string modelName, List<SearchObject> filters) where T : class
        {
            if (filters != null)
            {
                RouteDepht(ref filters);

                foreach (var item in filters)
                {
                    var additionalFilter = PredicateBuilder.True<T>();
                    string propertyName = item.Property;

                    //if we want to search same stuff in different properties with OR (1 field on frontend) 
                    if(propertyName.Contains(";"))
                    {
                        List<SearchObject> orFilters = new List<SearchObject>();
                        string[] properties = propertyName.Split(';');
                        foreach (var property in properties)
                        {
                            orFilters.Add(new SearchObject(property.Trim(), item.Values, (SearchOptions)item.SearchOptionId, (PropertyDataTypes)item.PropertyDataTypeId));
                        }
                        additionalFilter = PredicateBuilder.False<T>();
                        BuildOrPredicate(ref additionalFilter, modelName, orFilters);
                        predicate = predicate.And(additionalFilter);
                    }
                    else
                    { 
                        string[] properties = propertyName.Split('.');
                        ParameterExpression model = Expression.Parameter(typeof(T), modelName);

                        //maybe it's a crutch. this flag allow us to build search clause only once if we have more than 1 collection on the way
                        bool buildSearchClause = true;
                        bool buildSearchClauseInSimpleMemberAccess = true;
                        List<Expression> forComplexTypes = null;

                        Expression property = 
                        BuildAccessors(ref additionalFilter, model, model, item, properties, 0, ref buildSearchClause, ref buildSearchClauseInSimpleMemberAccess, ref forComplexTypes);

                        //and this usage is a real crutch, because if we had no collection in the way, filter was set in BuildAccessors method and this code will just crash
                        if (!buildSearchClause)
                        {
                            additionalFilter = Expression.Lambda<Func<T, bool>>(property, model);
                        }

                        predicate = predicate.And(additionalFilter);
                    }
                }
            }
        }

        private static Expression BuildAccessors<T>(ref Expression<Func<T, bool>> additionalFilter, ParameterExpression model, 
            Expression parent, SearchObject item, string[] properties, int index, ref bool buildSearchClause, ref bool buildSearchClauseInSimpleMemberAccess, ref List<Expression> forComplexTypes) 
            where T : class
        {                                 
            if (index < properties.Length)
            {
                string member = properties[index];   

                // If it's collection, then we need to do something more complicated than simple property assign
                if (typeof(IEnumerable).IsAssignableFrom(parent.Type) && parent.Type != typeof(string))
                {
                    buildSearchClauseInSimpleMemberAccess = false;
                    // input eg: Foo.Bars (type ICollection<Bar>), output: type Bar
                    Type enumerableType = parent.Type.GetGenericArguments().SingleOrDefault();

                    // declare parameter for the lambda expression of Foo.Select(x => x.BarID)
                    ParameterExpression param = Expression.Parameter(enumerableType, member);

                    // Recurse to build the inside of the lambda, so x => x.BarID.
                    Expression lambdaBody = BuildAccessors(ref additionalFilter, model, param, item, properties, index, ref buildSearchClause, ref buildSearchClauseInSimpleMemberAccess, ref forComplexTypes);
                    
                    // Need to get method Enumerable.Any(), but it's generic, so there's no other way now.
                    MethodInfo anyMethod = typeof(Enumerable).GetMethods().Where(x => x.Name == "Any" && x.GetParameters().Length == 2).Single().MakeGenericMethod(enumerableType);
                                        
                    MethodCallExpression invokeAny;                                        
                    if (buildSearchClause)
                    {
                        //строим условие поиска внутри Any
                        //это выполняется на последнем уровне рекурсии, если у нас была на пути коллекция
                        MethodInfo predicateBuilder = typeof(PredicateBuilder).GetMethods().Where(x => x.Name == "True").Single().MakeGenericMethod(enumerableType);
                        Expression innerFilter = (Expression)predicateBuilder.Invoke(null, null);
                        MethodInfo searchBuilder = typeof(FilterBuilder).GetMethods().Where(x => x.Name == "BuildSearchClause").Single().MakeGenericMethod(enumerableType);
                        object[] args = new object[] { innerFilter, param, item, lambdaBody, forComplexTypes };
                        searchBuilder.Invoke(null, args);
                        innerFilter = args[0] as Expression;
                        buildSearchClause = false;
                        invokeAny = Expression.Call(null, anyMethod, parent, innerFilter);
                    }
                    else
                    {
                        //а тут мы строим Any без добавления к нему лишних условий
                        Type funcType = typeof(Func<,>).MakeGenericType(enumerableType, lambdaBody.Type);
                        LambdaExpression lambda = Expression.Lambda(funcType, lambdaBody, param);
                        invokeAny = Expression.Call(null, anyMethod, parent, lambda);
                    }

                    return invokeAny;
                }
                else
                {
                    // Simply access a non-collection property
                    MemberExpression newParent = Expression.PropertyOrField(parent, member);

                    //Container crunch
                    if (member == "OwnerCode")
                    {
                        forComplexTypes = new List<Expression>();
                        forComplexTypes.Add(Expression.PropertyOrField(parent, "ContainerTypeCode"));
                        forComplexTypes.Add(Expression.PropertyOrField(parent, "DigitCode"));
                        forComplexTypes.Add(Expression.PropertyOrField(parent, "CheckNumber"));
                    }
                    //Route crunch
                    if (member == "TransportationPointTableName")
                    {
                        forComplexTypes = new List<Expression>();
                        forComplexTypes.Add(Expression.PropertyOrField(parent, "TransportationPointTableId"));
                        forComplexTypes.Add(Expression.PropertyOrField(parent, "Tail"));
                    }

                    //REWRITE THIS PART SOMEHOW to return data for filter, not to make it
                    //building search clause if there is no collection on our way
                    if ((index + 1) == properties.Length && buildSearchClauseInSimpleMemberAccess)
                    {
                        BuildSearchClause(ref additionalFilter, model, item, newParent, ref forComplexTypes);
                    }

                    // Recurse
                    return BuildAccessors(ref additionalFilter, model, newParent, item, properties, ++index, ref buildSearchClause, ref buildSearchClauseInSimpleMemberAccess, ref forComplexTypes);
                }
            }
            else
            {
                // Return the final expression once we've done recursing. Usable only if there was collection on our way
                //if(!buildSearchClauseInSimpleMemberAccess)
                //{
                //    parent = additionalFilter = Expression.Lambda<Func<T, bool>>(parent, model);
                //}
                return parent;
            }
        }

        public static void BuildSearchClause<T>(ref Expression<Func<T, bool>> filter, ParameterExpression model, SearchObject item, Expression property, ref List<Expression> forComplexTypes) where T : class
        {
            string rawString = string.Empty;
            List<string> filterStrings = null;
            Type type = property.Type;
            MethodInfo searchMethodCall = null;

            //string stuff
            switch (item.PropertyDataTypeId) 
            {
                //case 1:
                //    type = typeof(int?);
                //    break;
                //case 2:
                //    type = typeof(decimal?);
                //    break;
                //case 3:
                //    type = typeof(int?);
                //    break;
                //case 4:
                //    type = typeof(DateTime?);
                //    break;
                case 5:
                    //type = typeof(string);
                    foreach (var element in item.Values)
                    {
                        rawString += Convert.ToString(element) + ';';
                    }
                    filterStrings = rawString.Split(new Char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    searchMethodCall = typeof(string).GetMethod("Contains", new[] { typeof(string) });
                    break;
                //case 7:
                //    type = typeof(bool?);
                //    break;
                case 8: //Containers
                    type = typeof(string);
                    break;
                case 9: //Routes
                    type = typeof(string);
                    break;
                default:
                    break;
            }

            switch ((SearchOptions)item.SearchOptionId)
            {
                case SearchOptions.Equal:
                    {
                        filter = PredicateBuilder.False<T>();
                       
                        foreach (var subitem in item.Values)
                        {
                            ConstantExpression income;
                            //this condition for special office filter in organizations, coz we pass int?[] in Values but var in foreach has type int, and it can't be casted to int?                        
                            if (subitem is int)
                            {                                
                                income = Expression.Constant(subitem, type);
                            }
                            else
                            {
                                var temp = Convert.ChangeType(subitem, type);
                                income = Expression.Constant(temp, type);
                            }                            
                            BinaryExpression addPredicate = Expression.Equal(property, income);
                            Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                            filter = filter.Or(lambda);
                        }                        
                    }
                    break;
                case SearchOptions.NotEqual:
                    {
                        foreach (var subitem in item.Values)
                        {
                            var temp = Convert.ChangeType(subitem, type);
                            ConstantExpression income = Expression.Constant(temp, type);
                            BinaryExpression addPredicate = Expression.NotEqual(property, income);
                            Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                            filter = filter.And(lambda);
                        }
                    }
                    break;
                case SearchOptions.GreaterThan:
                    {
                        foreach (var subitem in item.Values)
                        {
                            var temp = Convert.ChangeType(subitem, type);
                            ConstantExpression income = Expression.Constant(temp, type);
                            BinaryExpression addPredicate = Expression.GreaterThan(property, income);
                            Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                            filter = filter.And(lambda);
                        }
                    }
                    break;
                case SearchOptions.GreaterThanOrEqual:
                    {
                        foreach (var subitem in item.Values)
                        {
                            ConstantExpression income;
                            if (subitem is DateTime)
                            {
                                income = Expression.Constant(subitem, type);
                            }
                            else
                            {
                                var temp = Convert.ChangeType(subitem, type);
                                income = Expression.Constant(temp, type);
                            }                            
                            BinaryExpression addPredicate = Expression.GreaterThanOrEqual(property, income);
                            Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                            filter = filter.And(lambda);
                        }
                    }
                    break;
                case SearchOptions.LessThan:
                    {
                        foreach (var subitem in item.Values)
                        {
                            ConstantExpression income;
                            if (subitem is DateTime)
                            {
                                income = Expression.Constant(subitem, type);
                            }
                            else
                            {
                                var temp = Convert.ChangeType(subitem, type);
                                income = Expression.Constant(temp, type);
                            }

                            //var temp = Convert.ChangeType(subitem, type);
                            //ConstantExpression income = Expression.Constant(temp, type);
                            BinaryExpression addPredicate = Expression.LessThan(property, income);
                            Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                            filter = filter.And(lambda);
                        }
                    }
                    break;
                case SearchOptions.LessThanOrEqual:
                    {
                        foreach (var subitem in item.Values)
                        {
                            var temp = Convert.ChangeType(subitem, type);
                            ConstantExpression income = Expression.Constant(temp, type);
                            BinaryExpression addPredicate = Expression.LessThanOrEqual(property, income);
                            Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                            filter = filter.And(lambda);
                        }
                    }
                    break;
                case SearchOptions.GreaterThan_LessThan:
                    {
                        ConstantExpression first = null;
                        ConstantExpression last = null;
                        foreach (var subitem in item.Values)
                        {
                            var temp = Convert.ChangeType(subitem, type);
                            if (first == null)
                            {
                                first = Expression.Constant(temp, type);
                                BinaryExpression addPredicate = Expression.GreaterThan(property, first);
                                Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                                filter = filter.And(lambda);
                            }
                            else
                            {
                                last = Expression.Constant(temp, type);
                                BinaryExpression addPredicate = Expression.LessThan(property, last);
                                Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                                filter = filter.And(lambda);
                            }
                        }
                    }
                    break;
                case SearchOptions.GreaterThanOrEqual_LessThanOrEqual:
                    {
                        ConstantExpression first = null;
                        ConstantExpression last = null;
                        foreach (var subitem in item.Values)
                        {
                            var temp = Convert.ChangeType(subitem, type);
                            if (first == null)
                            {
                                first = Expression.Constant(temp, type);
                                BinaryExpression addPredicate = Expression.GreaterThanOrEqual(property, first);
                                Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                                filter = filter.And(lambda);
                            }
                            else
                            {
                                last = Expression.Constant(temp, type);
                                BinaryExpression addPredicate = Expression.LessThanOrEqual(property, last);
                                Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                                filter = filter.And(lambda);
                            }
                        }
                    }
                    break;
                case SearchOptions.GreaterThanOrEqual_LessThan:
                    {
                        ConstantExpression first = null;
                        ConstantExpression last = null;
                        foreach (var subitem in item.Values)
                        {
                            var temp = Convert.ChangeType(subitem, type);
                            if (first == null)
                            {
                                first = Expression.Constant(temp, type);
                                BinaryExpression addPredicate = Expression.GreaterThanOrEqual(property, first);
                                Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                                filter = filter.And(lambda);
                            }
                            else
                            {
                                last = Expression.Constant(temp, type);
                                BinaryExpression addPredicate = Expression.LessThan(property, last);
                                Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                                filter = filter.And(lambda);
                            }
                        }
                    }
                    break;
                case SearchOptions.GreaterThan_LessThanOrEqual:
                    {
                        ConstantExpression first = null;
                        ConstantExpression last = null;
                        foreach (var subitem in item.Values)
                        {
                            var temp = Convert.ChangeType(subitem, type);
                            if (first == null)
                            {
                                first = Expression.Constant(temp, type);
                                BinaryExpression addPredicate = Expression.GreaterThan(property, first);
                                Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                                filter = filter.And(lambda);
                            }
                            else
                            {
                                last = Expression.Constant(temp, type);
                                BinaryExpression addPredicate = Expression.LessThanOrEqual(property, last);
                                Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                                filter = filter.And(lambda);
                            }
                        }
                    }
                    break;
                case SearchOptions.Contains:
                    {
                        filter = PredicateBuilder.False<T>();                        
                        foreach (var subitem in filterStrings)
                        {
                            ConstantExpression right = Expression.Constant(subitem.Trim(), type);
                            MethodCallExpression addPredicate = Expression.Call(property, searchMethodCall, right);
                            Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                            filter = filter.Or(lambda);
                        }
                    }
                    break;
                case SearchOptions.NotContains:
                    {
                        foreach (var subitem in filterStrings)
                        {
                            ConstantExpression right = Expression.Constant(subitem.Trim(), type);
                            MethodCallExpression addPredicate = Expression.Call(property, searchMethodCall, right);
                            Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(Expression.Not(addPredicate), model);
                            filter = filter.And(lambda);
                        }
                    }
                    break;
                case SearchOptions.True:
                    {
                        ConstantExpression right = Expression.Constant(true, type);
                        BinaryExpression addPredicate = Expression.Equal(property, right);
                        Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                        filter = filter.And(lambda);
                    }
                    break;
                case SearchOptions.False:
                    {
                        ConstantExpression right = Expression.Constant(false, type);
                        BinaryExpression addPredicate = Expression.Equal(property, right);
                        Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                        filter = filter.And(lambda);
                    }
                    break;
                case SearchOptions.Container:
                    {
                        filter = PredicateBuilder.False<T>();
                        foreach (var subitem in item.Values)
                        {
                            string temp = Convert.ChangeType(subitem, type);
                            temp = temp.Trim();
                            int dot = temp.IndexOf('.');
                            if (dot != -1)
                            {
                                temp = temp.Remove(dot, 1);
                            }

                            Type type2 = typeof(int?);

                            string ownerCode = temp.Substring(0, 3);
                            string containerTypeCode = temp.Substring(3, 1);
                            int digitCode = Convert.ToInt32(temp.Substring(4, 6));
                            int checkNumber = Convert.ToInt32(temp.Substring(10, 1));

                            ConstantExpression incomeOwnerCode = Expression.Constant(ownerCode, type);
                            ConstantExpression incomeContainerTypeCode = Expression.Constant(containerTypeCode, type);
                            ConstantExpression incomeDigitCode = Expression.Constant(digitCode, type2);
                            ConstantExpression incomeCheckNumber = Expression.Constant(checkNumber, type2);
                                                        
                            Expression containerTypeCodeMember = forComplexTypes[0];
                            Expression digitCodeMember = forComplexTypes[1];
                            Expression checkNumberMember = forComplexTypes[2];

                            BinaryExpression addPredicate10 = Expression.Equal(property, incomeOwnerCode);
                            BinaryExpression addPredicate20 = Expression.Equal(containerTypeCodeMember, incomeContainerTypeCode);
                            BinaryExpression addPredicate30 = Expression.Equal(digitCodeMember, incomeDigitCode);
                            BinaryExpression addPredicate40 = Expression.Equal(checkNumberMember, incomeCheckNumber);
                                                        
                            BinaryExpression addPredicate = Expression.And(addPredicate10, addPredicate20);
                            addPredicate = Expression.And(addPredicate, addPredicate30);
                            addPredicate = Expression.And(addPredicate, addPredicate40);

                            Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                            filter = filter.Or(lambda);
                        }
                    }
                    break;
                case SearchOptions.Route:
                    {
                        filter = PredicateBuilder.False<T>();
                        foreach (var subitem in item.Values)
                        {
                            string temp = Convert.ChangeType(subitem, type);
                            temp = temp.Trim();

                            filterStrings = temp.Split(new Char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                            Type type2 = typeof(int?);
                            Type type3 = typeof(byte?);

                            string tableName = filterStrings[0];
                            int tableId = Convert.ToInt32(filterStrings[1]);
                            byte tail = Convert.ToByte(filterStrings[2]);

                            ConstantExpression incomeTableName = Expression.Constant(tableName, type);
                            ConstantExpression incomeTableId = Expression.Constant(tableId, type2);
                            ConstantExpression incomeTail = Expression.Constant(tail, type3);

                            Expression tableIdMember = forComplexTypes[0];
                            Expression tailMember = forComplexTypes[1];

                            BinaryExpression addPredicate10 = Expression.Equal(property, incomeTableName);
                            BinaryExpression addPredicate20 = Expression.Equal(tableIdMember, incomeTableId);
                            BinaryExpression addPredicate30 = Expression.Equal(tailMember, incomeTail);

                            BinaryExpression addPredicate = Expression.And(addPredicate10, addPredicate20);
                            addPredicate = Expression.And(addPredicate, addPredicate30);

                            Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                            filter = filter.Or(lambda);
                        }                    
                    }
                    break;
                case SearchOptions.RoutePoint:
                    {
                        filter = PredicateBuilder.False<T>();
                        foreach (var subitem in item.Values)
                        {
                            string temp = Convert.ChangeType(subitem, type);
                            temp = temp.Trim();

                            filterStrings = temp.Split(new Char[] { ';' }, StringSplitOptions.RemoveEmptyEntries).ToList();

                            Type type2 = typeof(int?);

                            string tableName = filterStrings[0];                            
                            int tableId = Convert.ToInt32(filterStrings[1]);
                            
                            ConstantExpression incomeTableName = Expression.Constant(tableName, type);
                            ConstantExpression incomeTableId = Expression.Constant(tableId, type2);
                            
                            Expression tableIdMember = forComplexTypes[0];
                            
                            BinaryExpression addPredicate10 = Expression.Equal(property, incomeTableName);
                            BinaryExpression addPredicate20 = Expression.Equal(tableIdMember, incomeTableId);
                            
                            BinaryExpression addPredicate = Expression.And(addPredicate10, addPredicate20);
                            
                            Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                            filter = filter.Or(lambda);
                        }
                    }
                    break;
                case SearchOptions.IsNull:
                    {
                        //filter = PredicateBuilder.False<T>();
                        ConstantExpression right = Expression.Constant(null, type);
                        BinaryExpression addPredicate = Expression.Equal(property, right);
                        Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                        filter = filter.And(lambda);
                    }
                    break;
                case SearchOptions.NotNull:
                    {
                        ConstantExpression right = Expression.Constant(null, type);
                        BinaryExpression addPredicate = Expression.NotEqual(property, right);
                        Expression<Func<T, bool>> lambda = Expression.Lambda<Func<T, bool>>(addPredicate, model);
                        filter = filter.And(lambda);
                    }
                    break;
                case SearchOptions.CollectionFilled:
                    {

                    }
                    break;
                default:
                    break;
            }
        }

        //needed to search in our routes. Help us to join routepoint on itself to find points on 1 route
        public static void RouteDepht (ref List<SearchObject> filters)
        {
            if (filters.Any(d => d.SearchOptionId == 16))
            {
                SearchObject old = filters.Where(d => d.SearchOptionId == 16).FirstOrDefault();
                filters.Remove(old);
                SearchObject newOne = new SearchObject()
                {
                    Property = old.Property,
                    Values = new[] { old.Values[0] },
                    SearchOptionId = old.SearchOptionId,
                    PropertyDataTypeId = old.PropertyDataTypeId
                };
                SearchObject newTwo = new SearchObject()
                {
                    Property = old.Property.Replace("TransportationPointTableName", "Route.RoutePoint.TransportationPointTableName"),
                    Values = new[] { old.Values[1] },
                    SearchOptionId = old.SearchOptionId,
                    PropertyDataTypeId = old.PropertyDataTypeId
                };
                filters.Add(newOne);
                filters.Add(newTwo);
            }
        }

        //used for some special conditions (like Organizations office filter)
        public static void BuildOrPredicate<T>(ref Expression<Func<T, bool>> predicate, string modelName, List<SearchObject> filters) where T : class
        {
            if (filters != null)
            {
                RouteDepht(ref filters);

                foreach (var item in filters)
                {
                    var additionalFilter = PredicateBuilder.True<T>();
                    string propertyName = item.Property;
                    string[] properties = propertyName.Split('.');
                    ParameterExpression model = Expression.Parameter(typeof(T), modelName);

                    //maybe it's a crutch. this flag allow us to build search clause only once if we have more than 1 collection on the way
                    bool buildSearchClause = true;
                    bool buildSearchClauseInSimpleMemberAccess = true;
                    List<Expression> forComplexTypes = null;

                    Expression property = BuildAccessors(ref additionalFilter, model, model, item, properties, 0, ref buildSearchClause, ref buildSearchClauseInSimpleMemberAccess, ref forComplexTypes);

                    //and this usage is a real crutch, because if we had no collection in the way, filter was set in BuildAccessors method and this code will just crash
                    if (!buildSearchClause)
                    {
                        additionalFilter = Expression.Lambda<Func<T, bool>>(property, model);
                    }

                    predicate = predicate.Or(additionalFilter);
                }
            }
        }
    }
}