using System;
using MarkMpn.XmlSerializerAutoComplete.Tests.Model;
using Xunit;

namespace MarkMpn.XmlSerializerAutoComplete.Tests
{
    public class Tests
    {
        [Fact]
        public void ParsesFullDocument()
        {
            var xml = @"
                <?xml version='1.0' encoding='UTF-8' ?>
                <MyDoc>
                    <Members>
                        <p surname='s1'>
                            <forename>f1</forename>
                            <Age>40</Age>
                        </p>
                        <c surname='s2'>
                            <forename>f2</forename>
                            <Age>10</Age>
                            <FavouriteToy>t2</FavouriteToy>
                        </c>
                    </Members>
                    <Staff surname='s3'>
                        <forename>f3</forename>
                        <Age>41</Age>
                    </Staff>
                    <Staff xsi:type='Child' surname='s4'>
                        <forename>f4</forename>
                        <Age>11</Age>
                    </Staff>
                </MyDoc>";

            var autocomplete = new AutoComplete<Root>(xml);

            var root = Assert.IsType<Root>(autocomplete.ParsedObject);
            Assert.Equal(new Person[]
            {
                new Person
                {
                    FirstName = "f1",
                    LastName = "s1",
                    Age = 40
                },
                new Child
                {
                    FirstName = "f2",
                    LastName = "s2",
                    Age = 10,
                    FavouriteToy = "t2"
                }
            }, root.Clients, new PropertyComparer());
            Assert.Equal(new Person[]
            {
                new Person
                {
                    FirstName = "f3",
                    LastName = "s3",
                    Age = 41
                },
                new Child
                {
                    FirstName = "f4",
                    LastName = "s4",
                    Age = 11
                }
            }, root.Staff, new PropertyComparer());
        }

        [Fact]
        public void ParsesPartialDocument()
        {
            var xml = @"
                <?xml version='1.0' encoding='UTF-8' ?>
                <MyDoc>
                    <Members>
                        <p surname='s1'>
                            <forename>f1</forename>
                            <Age>40</Age>
                        </p>
                        <c surname='s2'>
                            <forename>f2</forename>
                            <Age>10</Age>
                            <FavouriteToy>t";

            var autocomplete = new AutoComplete<Root>(xml);

            var root = Assert.IsType<Root>(autocomplete.ParsedObject);
            Assert.Equal(new Person[]
            {
                new Person
                {
                    FirstName = "f1",
                    LastName = "s1",
                    Age = 40
                },
                new Child
                {
                    FirstName = "f2",
                    LastName = "s2",
                    Age = 10,
                    FavouriteToy = "t"
                }
            }, root.Clients, new PropertyComparer());
            Assert.Null(root.Staff);
        }

        [Fact]
        public void AutoSuggestsRootElement()
        {
            var autocomplete = new AutoComplete<Root>("<");

            Assert.Equal(new AutocompleteSuggestion[]
            {
                new AutocompleteElementSuggestion
                {
                    Name = "MyDoc"
                }
            }, autocomplete.Suggestions, new PropertyComparer());
        }

        [Fact]
        public void AutoSuggestsPropertyElements()
        {
            var autocomplete = new AutoComplete<Root>("<MyDoc><");

            Assert.Equal(new AutocompleteSuggestion[]
            {
                new AutocompleteElementSuggestion{Name = "Members"},
                new AutocompleteElementSuggestion{Name = "Staff"},
            }, autocomplete.Suggestions, new PropertyComparer());
        }

        [Fact]
        public void AutoSuggestsPropertyAttributes()
        {
            var autocomplete = new AutoComplete<Root>("<MyDoc><Members><p ");

            Assert.Equal(new AutocompleteSuggestion[]
            {
                new AutocompleteAttributeSuggestion{Name = "surname"}
            }, autocomplete.Suggestions, new PropertyComparer());
        }

        [Fact]
        public void HandlesRecursiveModels()
        {
            var xml = @"
                <?xml version='1.0' encoding='UTF-8' ?>
                <Recursive>
                    <Name>1</Name>
                    <Child>
                        <Name>2</Name>
                        <Child>
                            <Name>3</Name>
                        </Child>
                    </Child>
                </Recursive>";

            var autocomplete = new AutoComplete<Recursive>(xml);

            var root = Assert.IsType<Recursive>(autocomplete.ParsedObject);
            Assert.Equal("1", root.Name);
            Assert.Equal("2", root?.Child.Name);
            Assert.Equal("3", root?.Child?.Child.Name);
        }
    }
}
