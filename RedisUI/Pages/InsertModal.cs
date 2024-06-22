namespace RedisUI.Pages
{
    public static class InsertModal
    {
        public static string Build()
        {

            return $@"
<div class=""modal fade"" id=""insertModal"" tabindex=""-1"" aria-labelledby=""insertModalLabel"" aria-hidden=""true"">
  <div class=""modal-dialog"">
    <div class=""modal-content"">
      <div class=""modal-header"">
        <h1 class=""modal-title fs-5"" id=""insertModalLabel"">Add or Edit Key</h1>
        <button type=""button"" class=""btn-close"" data-bs-dismiss=""modal"" aria-label=""Close""></button>
      </div>
      <div class=""modal-body"">
        <div class=""mb-3"">
          <input type=""text"" class=""form-control"" id=""insertKey"" placeholder=""Key"" onkeyup=""checkRequired()"">
        </div>
        <div class=""mb-3"">
          <textarea rows=""10"" type=""text"" class=""form-control"" id=""insertValue"" placeholder=""Value"" onkeyup=""checkRequired()""></textarea>
        </div>
      </div>
      <div class=""modal-footer"">
        <button type=""button"" class=""btn btn-secondary"" data-bs-dismiss=""modal"">Close</button>
        <button type=""button"" class=""btn btn-primary"" onclick=""saveKey()"" id=""btnSave"" disabled>Save</button>
      </div>
    </div>
  </div>
</div>";
        }
    }
}
