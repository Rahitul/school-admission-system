using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SchoolAdmission.Tests;

public class AdmissionTests : IClassFixture<TestFactory>, IAsyncLifetime
{
    private readonly TestFactory _factory;
    private readonly HttpClient _client;

    public AdmissionTests(TestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await DbCleaner.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    private static string Student(int i) => ("1" + i.ToString().PadLeft(16, '0')).Substring(0, 17);

    [Fact]
    public async Task B1_Concurrency_NoOverAllocation()
    {
        // create class with 10 seats
        var createRes = await _client.PostAsJsonAsync("/classes", new { name = "C1", totalSeats = 10 });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var cls = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var classId = cls.GetProperty("id").GetString()!;

        var tasks = Enumerable.Range(0, 200).Select(i =>
        {
            var c = _factory.CreateClient();
            c.DefaultRequestHeaders.Add("X-Student-Id", Student(i));
            return c.PostAsync($"/classes/{classId}/holds", null);
        }).ToArray();

        var results = await Task.WhenAll(tasks);
        var created = results.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflict = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(10, created);
        Assert.Equal(190, conflict);

        var getRes = await _client.GetAsync($"/classes/{classId}");
        var info = await getRes.Content.ReadFromJsonAsync<JsonElement>();
        var held = info.GetProperty("heldSeats").GetInt32();
        var confirmed = info.GetProperty("confirmedSeats").GetInt32();
        var total = info.GetProperty("totalSeats").GetInt32();
        Assert.True(held + confirmed <= total);
        Assert.Equal(10, held + confirmed);
    }

    [Fact]
    public async Task B2_AlreadyApplied_And_ReapplyAfterCancel()
    {
        var createRes = await _client.PostAsJsonAsync("/classes", new { name = "C2", totalSeats = 5 });
        var cls = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var classId = cls.GetProperty("id").GetString()!;
        var sid = Student(1);

        var r1 = await PostHold(classId, sid);
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);

        var r2 = await PostHold(classId, sid);
        Assert.Equal(HttpStatusCode.Conflict, r2.StatusCode);
        var body2 = await r2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ALREADY_APPLIED", body2.GetProperty("error").GetProperty("code").GetString());

        // cancel
        var h1 = await r1.Content.ReadFromJsonAsync<JsonElement>();
        var holdId = h1.GetProperty("holdId").GetString()!;
        var del = await DeleteHold(holdId, sid);
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // reapply
        var r3 = await PostHold(classId, sid);
        Assert.Equal(HttpStatusCode.Created, r3.StatusCode);
    }

    [Fact]
    public async Task B3_Expiry()
    {
        var createRes = await _client.PostAsJsonAsync("/classes", new { name = "C3", totalSeats = 5 });
        var cls = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var classId = cls.GetProperty("id").GetString()!;
        var sid = Student(2);

        var r1 = await PostHold(classId, sid);
        var h1 = await r1.Content.ReadFromJsonAsync<JsonElement>();
        var holdId = h1.GetProperty("holdId").GetString()!;

        // wait for expiry (HOLD_DURATION_SECONDS=5)
        await Task.Delay(6000);

        // confirm should fail HOLD_EXPIRED
        var conf = await ConfirmHold(holdId, sid);
        Assert.Equal(HttpStatusCode.Conflict, conf.StatusCode);
        var cbody = await conf.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("HOLD_EXPIRED", cbody.GetProperty("error").GetProperty("code").GetString());

        // seat available again
        var info = await _client.GetAsync($"/classes/{classId}");
        var i = await info.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(5, i.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Confirm_Idempotent()
    {
        var createRes = await _client.PostAsJsonAsync("/classes", new { name = "C4", totalSeats = 5 });
        var cls = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var classId = cls.GetProperty("id").GetString()!;
        var sid = Student(3);

        var r1 = await PostHold(classId, sid);
        var h1 = await r1.Content.ReadFromJsonAsync<JsonElement>();
        var holdId = h1.GetProperty("holdId").GetString()!;

        var c1 = await ConfirmHold(holdId, sid);
        Assert.Equal(HttpStatusCode.OK, c1.StatusCode);

        var c2 = await ConfirmHold(holdId, sid);
        Assert.Equal(HttpStatusCode.OK, c2.StatusCode);

        var info = await _client.GetAsync($"/classes/{classId}");
        var i = await info.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, i.GetProperty("confirmedSeats").GetInt32());
        Assert.Equal(4, i.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public async Task Owner_Check_403()
    {
        var createRes = await _client.PostAsJsonAsync("/classes", new { name = "C5", totalSeats = 5 });
        var cls = await createRes.Content.ReadFromJsonAsync<JsonElement>();
        var classId = cls.GetProperty("id").GetString()!;
        var sid = Student(4);
        var other = Student(5);

        var r1 = await PostHold(classId, sid);
        var h1 = await r1.Content.ReadFromJsonAsync<JsonElement>();
        var holdId = h1.GetProperty("holdId").GetString()!;

        var conf = await ConfirmHold(holdId, other);
        Assert.Equal(HttpStatusCode.Forbidden, conf.StatusCode);
    }

    [Fact]
    public async Task Validation_4xx_Not500()
    {
        var r1 = await _client.PostAsJsonAsync("/classes", new { name = "", totalSeats = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, r1.StatusCode);

        var r2 = await _client.PostAsync("/classes/xxx/holds", null);
        Assert.Equal(HttpStatusCode.BadRequest, r2.StatusCode); // missing header

        var r3 = await _client.GetAsync("/classes/doesnotexist");
        Assert.Equal(HttpStatusCode.NotFound, r3.StatusCode);
    }

    private async Task<HttpResponseMessage> PostHold(string classId, string sid)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Student-Id", sid);
        return await c.PostAsync($"/classes/{classId}/holds", null);
    }

    private async Task<HttpResponseMessage> DeleteHold(string holdId, string sid)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Student-Id", sid);
        return await c.DeleteAsync($"/holds/{holdId}");
    }

    private async Task<HttpResponseMessage> ConfirmHold(string holdId, string sid)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Add("X-Student-Id", sid);
        return await c.PostAsync($"/holds/{holdId}/confirm", null);
    }
}