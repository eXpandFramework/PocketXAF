﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.DC;
using DevExpress.ExpressApp.DC.Xpo;
using DevExpress.ExpressApp.Model;
using DevExpress.ExpressApp.Utils;
using DevExpress.ExpressApp.Xpo;
using DevExpress.Xpo;
using DevExpress.Xpo.Metadata;
using Fasterflect;

namespace PocketXaf.SourcePackages.TypeInfoExtensions{
    public static class TypesInfoExtensions {
        public static IEnumerable<ITypeInfo> BaseInfos(this ITypeInfo typeInfo) {
            var baseInfo = typeInfo.Base;
            while (baseInfo != null) {
                yield return baseInfo;
                baseInfo = baseInfo.Base;
            }
        }

        public static IEnumerable<ITypeInfo> DomainSealedInfos(this ITypesInfo typesInfo,Type type){
            var typeInfo = typesInfo.FindTypeInfo(type);
            var infos = typeInfo.IsInterface ? typeInfo.Implementors : typeInfo.Descendants;
            var typeInfos = infos.Where(info => !info.IsAbstract).Reverse().ToArray();
            return typeInfos.Except(typeInfos.SelectMany(BaseInfos));
        }

        public static IEnumerable<ITypeInfo> DomainSealedInfos<T>(this ITypesInfo typesInfo){
            return typesInfo.DomainSealedInfos(typeof(T));
        }

        public static void ModifySequenceObjectWhenMySqlDatalayer(this ITypesInfo typesInfo) {
            throw new NotImplementedException();
//            SequenceGeneratorHelper.ModifySequenceObjectWhenMySqlDatalayer(typesInfo);
        }

        public static ITypeInfo GetTypeInfo(this object obj){
            return obj.GetType().GetTypeInfo();
        }

        public static ITypeInfo GetTypeInfo(this Type type){
            return XafTypesInfo.Instance.FindTypeInfo(type);
        }

        public static bool IsDomainComponent(this Type type){
            return type.Attribute<DomainComponentAttribute>() != null ||new ReflectionDictionary().QueryClassInfo(type) != null;
        }

        public static IModelClass ModelClass(this ITypeInfo typeInfo){
            return CaptionHelper.ApplicationModel.BOModel.GetClass(typeInfo.Type);
        }

        public static XPClassInfo QueryXPClassInfo(this ITypeInfo typeInfo){
            var typeInfoSource = ((TypeInfo)typeInfo).Source as XpoTypeInfoSource;
            return typeInfoSource?.XPDictionary.QueryClassInfo(typeInfo.Type);
        }

        public static XPClassInfo FindDCXPClassInfo(this ITypeInfo typeInfo) {
            throw new NotImplementedException();
//            var xpoTypeInfoSource = ((XpoTypeInfoSource) ((TypeInfo) typeInfo).Source);
//            if (InterfaceBuilder.RuntimeMode) {
//                var generatedEntityType = xpoTypeInfoSource.GetGeneratedEntityType(typeInfo.Type);
//                return generatedEntityType == null ? null : xpoTypeInfoSource.XPDictionary.GetClassInfo(generatedEntityType);
//            }
//            var className = typeInfo.Name + "BaseDCDesignTimeClass";
//            var xpClassInfo = xpoTypeInfoSource.XPDictionary.QueryClassInfo("", className);
//            return xpClassInfo ?? new XPDataObjectClassInfo(xpoTypeInfoSource.XPDictionary, className);
        }

        static readonly MemberSetter XpoTypeInfoSourceSetter = typeof(XpoTypesInfoHelper).DelegateForSetFieldValue("xpoTypeInfoSource");
        public static void AssignAsPersistentEntityStore(this XpoTypeInfoSource xpoTypeInfoSource){
            var entityStores = (List<IEntityStore>) XafTypesInfo.Instance.GetFieldValue("entityStores");
            entityStores.RemoveAll(store => store is XpoTypeInfoSource);
            XafTypesInfo.Instance.SetFieldValue("dcEntityStore", null);
            XafTypesInfo.Instance.GetFieldValue("entityTypesCache").CallMethod("Clear");
            ((TypesInfo) XafTypesInfo.Instance).AddEntityStore(xpoTypeInfoSource);
            XpoTypeInfoSourceSetter(null, xpoTypeInfoSource);
        }

        public static void AssignAsInstance(this ITypesInfo typesInfo) {
            Guard.ArgumentNotNull(typesInfo, "typesInfo");
            var type = typeof (XafTypesInfo);
            if (type.GetFieldValue("instance")!=typesInfo){
                type.SetFieldValue("instance", typesInfo);
                typeof(XpoTypesInfoHelper).SetFieldValue("xpoTypeInfoSource",((TypesInfo) typesInfo).FindEntityStore(typeof(XpoTypeInfoSource)));
            }
        }

        public static Type FindBussinessObjectType(this ITypesInfo typesInfo,Type type){
            if (!(type.IsInterface))
                return type;
            var implementors = typesInfo.FindTypeInfo(type).Implementors.ToArray();
            var objectType = implementors.FirstOrDefault();
            if (objectType == null)
                throw new ArgumentException("Add a business object that implements " +
                                            type.FullName + " at your AdditionalBusinessClasses (module.designer.cs)");
            if (implementors.Length > 1) {
                var typeInfos = implementors.Where(implementor => implementor.Base != null && !(type.IsAssignableFrom(implementor.Base.Type)));
                foreach (ITypeInfo implementor in typeInfos) {
                    return implementor.Type;
                }

                throw new ArgumentNullException("More than 1 objects implement " + type.FullName);
            }
            return objectType.Type;

        }

        public static Type FindBussinessObjectType<T>(this ITypesInfo typesInfo){
            return typesInfo.FindBussinessObjectType(typeof(T));
        }

        public static XPMemberInfo CreateMember(this ITypesInfo typesInfo, Type typeToCreateOn, Type typeOfMember, string associationName) {
            return CreateMember(typesInfo, typeToCreateOn, typeOfMember, associationName,  true);
        }

        public static XPMemberInfo CreateMember(this ITypesInfo typesInfo, Type typeToCreateOn, Type typeOfMember, string associationName,  bool refreshTypesInfo) {
            return CreateMember(typesInfo, typeToCreateOn, typeOfMember, associationName,  typeOfMember.Name, refreshTypesInfo);
        }

        public static XPMemberInfo CreateMember(this ITypesInfo typesInfo, Type typeToCreateOn, Type typeOfMember, string associationName,  string propertyName) {
            return CreateMember(typesInfo, typeToCreateOn, typeOfMember, associationName,  propertyName, true);
        }

        public static XPMemberInfo CreateMember(this ITypesInfo typesInfo, Type typeToCreateOn, Type typeOfMember, string associationName,  string propertyName, bool refreshTypesInfo) {
            XPMemberInfo member = null;
            if (TypeIsRegister(typesInfo, typeToCreateOn)) {
                XPClassInfo xpClassInfo = typesInfo.FindTypeInfo(typeToCreateOn).QueryXPClassInfo();
                member = xpClassInfo.FindMember(propertyName);
                if (member == null) {
                    member = xpClassInfo.CreateMember(propertyName, typeOfMember);
                    member.AddAttribute(new AssociationAttribute(associationName));

                    if (refreshTypesInfo)
                        typesInfo.RefreshInfo(typeToCreateOn);
                }
            }
            return member;
        }

        public static XPMemberInfo CreateCollection(this ITypesInfo typeInfo, Type typeToCreateOn, Type typeOfCollection, string associationName) {
            return CreateCollection(typeInfo, typeToCreateOn, typeOfCollection, associationName,  true);
        }

        static XPMemberInfo CreateCollection(this ITypesInfo typeInfo, Type typeToCreateOn, Type typeOfCollection, string associationName,  bool refreshTypesInfo,
            string propertyName, bool isManyToMany) {
            return CreateCollection(typeInfo, typeToCreateOn, typeOfCollection, associationName, propertyName, refreshTypesInfo, isManyToMany);
        }

        public static XPMemberInfo CreateCollection(this ITypesInfo typeInfo, Type typeToCreateOn, Type typeOfCollection, string associationName,  bool refreshTypesInfo,
            string propertyName) {
            return CreateCollection(typeInfo, typeToCreateOn, typeOfCollection, associationName,  propertyName, refreshTypesInfo, false);
        }

        public static XPMemberInfo CreateCollection(this ITypesInfo typeInfo, Type typeToCreateOn, Type typeOfCollection, string associationName,  bool refreshTypesInfo) {
            return CreateCollection(typeInfo, typeToCreateOn, typeOfCollection, associationName,  refreshTypesInfo, typeOfCollection.Name + "s");
        }

        public static XPMemberInfo CreateCollection(this ITypesInfo typeInfo, Type typeToCreateOn, Type typeOfCollection, string associationName,  string collectionName) {
            return CreateCollection(typeInfo, typeToCreateOn, typeOfCollection, associationName,  collectionName, true);
        }

        static XPMemberInfo CreateCollection(this ITypesInfo typeInfo, Type typeToCreateOn, Type typeOfCollection, string associationName,  string collectionName, bool refreshTypesInfo,
            bool isManyToMany) {
            XPMemberInfo member = null;
            if (TypeIsRegister(typeInfo, typeToCreateOn)) {
                XPClassInfo xpClassInfo = typeInfo.FindTypeInfo(typeToCreateOn).QueryXPClassInfo();
                member = xpClassInfo.FindMember(collectionName) ??
                         xpClassInfo.CreateMember(collectionName, typeof(XPCollection), true);
                if (member.FindAttributeInfo(typeof(AssociationAttribute))==null)
                    member.AddAttribute(new AssociationAttribute(associationName, typeOfCollection){
                        UseAssociationNameAsIntermediateTableName = isManyToMany
                    });
                if (refreshTypesInfo)
                    typeInfo.RefreshInfo(typeToCreateOn);
            }
            return member;

        }
        public static XPMemberInfo CreateCollection(this ITypesInfo typeInfo, Type typeToCreateOn, Type typeOfCollection, string associationName,  string collectionName, bool refreshTypesInfo) {
            return CreateCollection(typeInfo, typeToCreateOn, typeOfCollection, associationName,  collectionName, refreshTypesInfo, false);
        }

        public static List<XPMemberInfo> CreateBothPartMembers(this ITypesInfo typesInfo, Type typeToCreateOn, Type otherPartType) {
            return CreateBothPartMembers(typesInfo, typeToCreateOn, otherPartType,  false);
        }

        public static List<XPMemberInfo> CreateBothPartMembers(this ITypesInfo typesinfo, Type typeToCreateOn, Type otherPartMember,  bool isManyToMany) {
            return CreateBothPartMembers(typesinfo, typeToCreateOn, otherPartMember, isManyToMany, Guid.NewGuid().ToString());
        }

        public static List<XPMemberInfo> CreateBothPartMembers(this ITypesInfo typesinfo, Type typeToCreateOn, Type otherPartMember, bool isManyToMany, string association,
            string createOnPropertyName, string otherPartPropertyName) {
            var infos = new List<XPMemberInfo>();
            XPMemberInfo member = isManyToMany
                ? CreateCollection(typesinfo, typeToCreateOn, otherPartMember, association, false, createOnPropertyName, true)
                : CreateMember(typesinfo, typeToCreateOn, otherPartMember, association,  createOnPropertyName, false);

            if (member != null) {
                infos.Add(member);
                member = isManyToMany
                    ? CreateCollection(typesinfo, otherPartMember, typeToCreateOn, association, false, otherPartPropertyName, true)
                    : CreateCollection(typesinfo, typeToCreateOn, otherPartMember, association, false, otherPartPropertyName);

                if (member != null)
                    infos.Add(member);
            }

            typesinfo.RefreshInfo(typeToCreateOn);
            typesinfo.RefreshInfo(otherPartMember);
            return infos;

        }

        public static List<XPMemberInfo> CreateBothPartMembers(this ITypesInfo typesinfo, Type typeToCreateOn, Type otherPartMember,  bool isManyToMany, string association) {

            var infos = new List<XPMemberInfo>();
            XPMemberInfo member = isManyToMany
                ? CreateCollection(typesinfo, typeToCreateOn, otherPartMember, association,  false)
                : CreateMember(typesinfo, otherPartMember, typeToCreateOn, association,  false);

            if (member != null) {
                infos.Add(member);
                member = isManyToMany
                    ? CreateCollection(typesinfo, otherPartMember, typeToCreateOn, association, false)
                    : CreateCollection(typesinfo, typeToCreateOn, otherPartMember, association, false);

                if (member != null)
                    infos.Add(member);
            }

            typesinfo.RefreshInfo(typeToCreateOn);
            typesinfo.RefreshInfo(otherPartMember);

            return infos;
        }

        public static ITypeInfo FindTypeInfo<T>(this ITypesInfo typesInfo) {
            return typesInfo.FindTypeInfo(typeof(T));
        }

        public static void AddAttribute<T>(this ITypeInfo typeInfo, Expression<Func<T, object>> expression, Attribute attribute) {
            IMemberInfo controlTypeMemberInfo = typeInfo.FindMember(expression);
            controlTypeMemberInfo.AddAttribute(attribute);
        }

        public static IMemberInfo FindMember<T>(this ITypeInfo typesInfo, Expression<Func<T, object>> expression) {
            throw new NotImplementedException();
//            MemberInfo memberInfo = expression.GetMemberInfo();
//            return typesInfo.FindMember(memberInfo.Name);
        }

        private static bool TypeIsRegister(ITypesInfo typeInfo, Type typeToCreateOn) {
            return XafTypesInfo.Instance.FindTypeInfo(typeToCreateOn).IsDomainComponent ||
                   typeInfo.PersistentTypes.FirstOrDefault(info => info.Type == typeToCreateOn) != null;
        }
    }
}