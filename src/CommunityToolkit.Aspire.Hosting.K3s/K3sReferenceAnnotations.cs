// Both K3sClusterResource and K3sServiceEndpointResource implement
// IResourceWithConnectionString. The standard Aspire WithReference overload adds a
// ResourceRelationshipAnnotation to dependents, which the BeforeStartEvent subscriber
// in AddK3sCluster uses to detect and apply container-specific overrides.
// No custom marker annotations are needed.
