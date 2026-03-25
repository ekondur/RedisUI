namespace RedisUI.Pages
{
    public static class InsertModal
    {
        public static string Build() =>
            @"
<div class=""modal fade"" id=""insertModal"" tabindex=""-1"" aria-labelledby=""insertModalLabel"" aria-hidden=""true"">
  <div class=""modal-dialog"">
    <div class=""modal-content"">
      <div class=""modal-header"">
        <h1 class=""modal-title fs-5"" id=""insertModalLabel"">Add or Edit Key</h1>
        <button type=""button"" class=""btn-close"" data-bs-dismiss=""modal"" aria-label=""Close""></button>
      </div>
      <div class=""modal-body"">
        <div class=""mb-3"">
          <select class=""form-select"" id=""insertType"">
            <option value=""string"">String</option>
            <option value=""list"">List</option>
            <option value=""set"">Set</option>
            <option value=""hash"">Hash</option>
            <option value=""sortedset"">Sorted Set</option>
            <option value=""stream"">Stream</option>
          </select>
        </div>
        <div class=""mb-3"">
          <input type=""text"" class=""form-control"" id=""insertKey"" placeholder=""Key"">
        </div>
        <div class=""mb-3"" id=""fieldGroup"" style=""display:none"">
          <input type=""text"" class=""form-control"" id=""insertField"" placeholder=""Field"">
        </div>
        <div class=""mb-3"" id=""scoreGroup"" style=""display:none"">
          <input type=""number"" class=""form-control"" id=""insertScore"" placeholder=""Score"" step=""any"">
        </div>
        <div class=""mb-3"">
          <textarea rows=""8"" class=""form-control"" id=""insertValue"" placeholder=""Value""></textarea>
        </div>
        <div class=""mb-3"">
          <input type=""number"" class=""form-control"" id=""insertTTL"" placeholder=""TTL in seconds (optional)"" min=""1"">
        </div>
      </div>
      <div class=""modal-footer"">
        <button type=""button"" class=""btn btn-secondary"" data-bs-dismiss=""modal"">Close</button>
        <button type=""button"" class=""btn btn-primary"" id=""btnSave"" disabled>Save</button>
      </div>
    </div>
  </div>
</div>";
    }
}
