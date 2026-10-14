using WasteSubmitRegPackagingApi.Obligations.Models;
using WasteSubmitRegPackagingApi.Obligations.Services;

namespace WasteSubmitRegPackagingApi.Test.Obligations.Services;

public class LeaverJoinerCodeMapperTest
{
    [Theory]
    [InlineData("03")]
    [InlineData("3")]
    [InlineData(" 03 ")]
    [InlineData("PreviouslyNotObligatedProducerJoinedGroupAsPartOfRegistration")]
    [InlineData("previouslynotobligatedproducerjoinedgroupaspartofregistration")]
    public void Maps_a_joiner_code_given_as_a_number_or_a_name(string rawCode)
    {
        Assert.Equal(new MappedLeaverJoinerCode(3, EventAction.Joined), LeaverJoinerCodeMapper.Map(rawCode));
    }

    [Theory]
    [InlineData("16")]
    [InlineData("MergedWithAnotherOrganisation")]
    public void Maps_a_leaver_code_given_as_a_number_or_a_name(string rawCode)
    {
        Assert.Equal(new MappedLeaverJoinerCode(16, EventAction.Left), LeaverJoinerCodeMapper.Map(rawCode));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("18")]
    [InlineData("99")]
    [InlineData("0")]
    [InlineData("1,2")]
    [InlineData("not a code")]
    public void Maps_anything_else_to_null(string? rawCode)
    {
        Assert.Null(LeaverJoinerCodeMapper.Map(rawCode));
    }

    [Fact]
    public void Splits_codes_1_to_21_except_18_into_the_gov_uk_joiner_and_leaver_lists()
    {
        var mapped = Enumerable.Range(1, 21)
            .Where(code => code != 18)
            .Select(code => LeaverJoinerCodeMapper.Map(code.ToString("00")))
            .ToList();

        Assert.All(mapped, Assert.NotNull);
        Assert.Equal([1, 2, 3, 7, 9, 15, 17, 19, 20], mapped.Where(code => code!.Action == EventAction.Joined).Select(code => code!.Code));
        Assert.Equal([4, 5, 6, 8, 10, 11, 12, 13, 14, 16, 21], mapped.Where(code => code!.Action == EventAction.Left).Select(code => code!.Code));
    }
}
