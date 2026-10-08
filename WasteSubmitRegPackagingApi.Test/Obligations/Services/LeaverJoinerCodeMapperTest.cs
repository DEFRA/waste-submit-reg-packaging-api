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
        var mapped = LeaverJoinerCodeMapper.Map(rawCode);

        Assert.Equal(JoinerCode.PreviouslyNotObligatedProducerJoinedGroupAsPartOfRegistration, mapped.JoinerCode);
        Assert.Null(mapped.LeaverCode);
    }

    [Theory]
    [InlineData("16")]
    [InlineData("MergedWithAnotherOrganisation")]
    public void Maps_a_leaver_code_given_as_a_number_or_a_name(string rawCode)
    {
        var mapped = LeaverJoinerCodeMapper.Map(rawCode);

        Assert.Equal(LeaverCode.MergedWithAnotherOrganisation, mapped.LeaverCode);
        Assert.Null(mapped.JoinerCode);
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
    public void Maps_anything_else_to_no_code(string? rawCode)
    {
        Assert.Equal(MappedLeaverJoinerCode.None, LeaverJoinerCodeMapper.Map(rawCode));
    }

    [Fact]
    public void Every_code_from_1_to_21_except_18_is_either_a_joiner_or_a_leaver_code()
    {
        var unmapped = Enumerable.Range(1, 21)
            .Where(code => code != 18)
            .Where(code => LeaverJoinerCodeMapper.Map(code.ToString("00")) == MappedLeaverJoinerCode.None);

        Assert.Empty(unmapped);
    }
}