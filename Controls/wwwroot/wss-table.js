// setIndeterminate now lives in wss-checkbox.js -- EditBool.Indeterminate needed the identical
// checkbox-mixed-state behavior, so the implementation moved to a neutrally-named shared module
// instead of being duplicated here. Re-exported under the original name/path so Table.razor's
// existing "wss-table.js" import keeps resolving unchanged.
export { setIndeterminate } from './wss-checkbox.js';
