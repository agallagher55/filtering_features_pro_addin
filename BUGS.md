# Known Bugs

## Feature dataset features not added to map correctly

Features from a feature dataset aren't being added to a map correctly because of a file path error. The feature dataset name isn't being included in the file path.

- [ ] Debug file path construction for feature dataset items
- [ ] Ensure `FeatureDatasetName` is properly included in the path

## Fields not populating in the view

Sometimes fields aren't populating in the detail/list view.

- [ ] Identify conditions that cause fields to fail to populate
- [ ] Ensure all field values are correctly extracted and displayed
